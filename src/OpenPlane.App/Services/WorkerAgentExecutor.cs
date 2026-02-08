using System.Diagnostics;
using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.App.Services;

public sealed class WorkerAgentExecutor : IAgentExecutor, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim mutex = new(1, 1);
    private Process? process;
    private StreamWriter? processIn;
    private StreamReader? processOut;
    private readonly IWorkspaceSettingsStore workspaceSettingsStore;
    private readonly ICopilotExecutionService copilotExecutionService;
    private readonly IModelCatalogService modelCatalogService;
    private string? workerWorkspaceId;

    public WorkerAgentExecutor(
        IWorkspaceSettingsStore workspaceSettingsStore,
        ICopilotExecutionService copilotExecutionService,
        IModelCatalogService modelCatalogService)
    {
        this.workspaceSettingsStore = workspaceSettingsStore;
        this.copilotExecutionService = copilotExecutionService;
        this.modelCatalogService = modelCatalogService;
    }

    public async Task<string> ExecuteStepAsync(PlanStep step, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var workspaceId = await GetSelectedWorkspaceIdAsync(cancellationToken);
            if (!IsToolStep(step))
            {
                var selection = await modelCatalogService.GetModelSelectionAsync(workspaceId, cancellationToken);
                var model = string.IsNullOrWhiteSpace(selection.ModelId) ? "gpt-5-mini" : selection.ModelId;
                var prompt = BuildReasoningPrompt(step);
                return await copilotExecutionService.ExecutePromptAsync(prompt, model, [], cancellationToken);
            }

            await EnsureProcessAsync(cancellationToken);
            await EnsureWorkerHealthyAsync(workspaceId, cancellationToken);

            var request = new WorkerRequest(
                Guid.NewGuid().ToString("N"),
                WorkerRequestType.ExecuteStep,
                workspaceId,
                step,
                DateTimeOffset.UtcNow);

            var response = await SendRequestAsync(request, cancellationToken);
            if (response.Success)
            {
                return response.Output ?? string.Empty;
            }

            if (string.Equals(response.ErrorCode, "policy_violation", StringComparison.OrdinalIgnoreCase))
            {
                throw new PolicyViolationException(response.ErrorMessage ?? "Policy violation in worker.");
            }

            throw new InvalidOperationException(response.ErrorMessage ?? "Worker step execution failed.");
        }
        finally
        {
            mutex.Release();
        }
    }

    private async Task EnsureWorkerHealthyAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var ping = new WorkerRequest(
            Guid.NewGuid().ToString("N"),
            WorkerRequestType.Ping,
            workspaceId,
            null,
            DateTimeOffset.UtcNow);

        var response = await SendRequestAsync(ping, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? "Worker ping failed.");
        }
    }

    private async Task<WorkerResponse> SendRequestAsync(WorkerRequest request, CancellationToken cancellationToken)
    {
        if (processIn is null || processOut is null)
        {
            throw new InvalidOperationException("Worker process streams are unavailable.");
        }

        var payload = JsonSerializer.Serialize(request, JsonOptions);
        await processIn.WriteLineAsync(payload);
        await processIn.FlushAsync();

        var line = await processOut.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidOperationException("Worker returned an empty response.");
        }

        var response = JsonSerializer.Deserialize<WorkerResponse>(line, JsonOptions)
            ?? throw new InvalidOperationException("Worker response payload is invalid.");

        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Worker response ID mismatch.");
        }

        return response;
    }

    private async Task EnsureProcessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var selectedWorkspaceId = await GetSelectedWorkspaceIdAsync(cancellationToken);

        if (process is { HasExited: false } &&
            processIn is not null &&
            processOut is not null &&
            string.Equals(workerWorkspaceId, selectedWorkspaceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DisposeProcess();

        var (fileName, arguments) = ResolveAgentHostCommand();
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        startInfo.Environment["OPENPLANE_WORKSPACE_ID"] = selectedWorkspaceId;
        startInfo.Environment["OPENPLANE_WORKSPACE_POLICIES_PATH"] = ResolveWorkspacePoliciesPath();

        process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start OpenPlane.AgentHost worker process.");

        processIn = process.StandardInput;
        processOut = process.StandardOutput;
        workerWorkspaceId = selectedWorkspaceId;

    }

    private async Task<string> GetSelectedWorkspaceIdAsync(CancellationToken cancellationToken)
    {
        var settings = await workspaceSettingsStore.LoadAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(settings.SelectedWorkspaceId) ? "default" : settings.SelectedWorkspaceId.Trim();
    }

    private static (string FileName, string Arguments) ResolveAgentHostCommand()
    {
        var explicitPath = Environment.GetEnvironmentVariable("OPENPLANE_AGENTHOST_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? ("dotnet", Quote(explicitPath))
                : (explicitPath, string.Empty);
        }

        var root = FindRepoRoot();
        if (root is not null)
        {
            var dllPath = Path.Combine(root, "src", "OpenPlane.AgentHost", "bin", "Debug", "net10.0", "OpenPlane.AgentHost.dll");
            if (File.Exists(dllPath))
            {
                return ("dotnet", Quote(dllPath));
            }

            var appHostPath = Path.Combine(root, "src", "OpenPlane.AgentHost", "bin", "Debug", "net10.0", "OpenPlane.AgentHost");
            if (File.Exists(appHostPath))
            {
                return (appHostPath, string.Empty);
            }
        }

        throw new InvalidOperationException(
            "OpenPlane.AgentHost executable not found. Build the solution first or set OPENPLANE_AGENTHOST_PATH.");
    }

    private static string? FindRepoRoot()
    {
        foreach (var seed in EnumerateSearchSeeds())
        {
            var current = seed;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, "OpenPlane.sln")))
                {
                    return current;
                }

                var parent = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.Ordinal))
                {
                    break;
                }

                current = parent;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchSeeds()
    {
        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            yield return AppContext.BaseDirectory;
        }

        var cwd = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(cwd))
        {
            yield return cwd;
        }
    }

    private static string Quote(string value)
    {
        return value.Contains(' ') ? $"\"{value}\"" : value;
    }

    private static bool IsToolStep(PlanStep step)
    {
        var details = step.Details?.Trim();
        return !string.IsNullOrWhiteSpace(details) &&
               details.StartsWith("tool:", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReasoningPrompt(PlanStep step)
    {
        var details = step.Details?.Trim();
        if (string.IsNullOrWhiteSpace(details))
        {
            return step.Title;
        }

        return $"Plan step: {step.Title}{Environment.NewLine}{Environment.NewLine}{details}";
    }

    private static string ResolveWorkspacePoliciesPath()
    {
        var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenPlane");
        return Path.Combine(appDirectory, "workspace-policies.json");
    }

    private void DisposeProcess()
    {
        processIn?.Dispose();
        processOut?.Dispose();

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            process.Dispose();
        }

        process = null;
        processIn = null;
        processOut = null;
        workerWorkspaceId = null;
    }

    public ValueTask DisposeAsync()
    {
        DisposeProcess();
        mutex.Dispose();
        return ValueTask.CompletedTask;
    }
}
