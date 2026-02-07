using System.Diagnostics;
using System.Text.Json;
using OpenPlane.Core.Models;
using OpenPlane.Storage.Services;

namespace OpenPlane.IntegrationTests.Flows;

public sealed class AgentHostWorkspacePolicyFlowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ExecuteStep_SearchUsesPolicyFileFromOverridePath()
    {
        var repoRoot = FindRepoRoot();
        var hostProjectPath = Path.Combine(repoRoot, "src", "OpenPlane.AgentHost", "OpenPlane.AgentHost.csproj");
        await EnsureAgentHostBuiltAsync(hostProjectPath);

        var hostDllPath = Path.Combine(repoRoot, "src", "OpenPlane.AgentHost", "bin", "Debug", "net10.0", "OpenPlane.AgentHost.dll");
        Assert.True(File.Exists(hostDllPath), $"AgentHost DLL missing at: {hostDllPath}");

        var tempRoot = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        var grantedRoot = Path.Combine(tempRoot, "granted");
        Directory.CreateDirectory(grantedRoot);
        var targetFile = Path.Combine(grantedRoot, "a.txt");
        await File.WriteAllTextAsync(targetFile, "content");

        var policyPath = Path.Combine(tempRoot, "workspace-policies.json");
        var policyStore = new JsonWorkspacePolicyStore("OpenPlane", policyPath);
        var workspaceId = "workspace-e2e";
        await policyStore.SaveAsync(
            new WorkspacePolicy(
                workspaceId,
                [new PathGrant(grantedRoot, AllowRead: true, AllowWrite: false, AllowCreate: false)],
                new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase))),
            CancellationToken.None);

        var startInfo = new ProcessStartInfo("dotnet", $"\"{hostDllPath}\"")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["OPENPLANE_WORKSPACE_POLICIES_PATH"] = policyPath;

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        try
        {
            var request = new WorkerRequest(
                Guid.NewGuid().ToString("N"),
                WorkerRequestType.ExecuteStep,
                workspaceId,
                new PlanStep(Guid.NewGuid().ToString("N"), "List", $"tool:search|{grantedRoot}|*", SignificantAction: false),
                DateTimeOffset.UtcNow);

            var requestLine = JsonSerializer.Serialize(request, JsonOptions);
            await process!.StandardInput.WriteLineAsync(requestLine);
            await process.StandardInput.FlushAsync();

            var responseLine = await process.StandardOutput.ReadLineAsync();
            Assert.False(string.IsNullOrWhiteSpace(responseLine));

            var response = JsonSerializer.Deserialize<WorkerResponse>(responseLine!, JsonOptions);
            Assert.NotNull(response);
            Assert.True(response!.Success, response.ErrorMessage);
            Assert.Contains(targetFile, response.Output ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                if (!process!.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task ExecuteStep_ReadImageReturnsExtractOnlyOutput()
    {
        var repoRoot = FindRepoRoot();
        var hostProjectPath = Path.Combine(repoRoot, "src", "OpenPlane.AgentHost", "OpenPlane.AgentHost.csproj");
        await EnsureAgentHostBuiltAsync(hostProjectPath);

        var hostDllPath = Path.Combine(repoRoot, "src", "OpenPlane.AgentHost", "bin", "Debug", "net10.0", "OpenPlane.AgentHost.dll");
        Assert.True(File.Exists(hostDllPath), $"AgentHost DLL missing at: {hostDllPath}");

        var tempRoot = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        var grantedRoot = Path.Combine(tempRoot, "granted");
        Directory.CreateDirectory(grantedRoot);
        var imagePath = Path.Combine(grantedRoot, "img.png");
        await File.WriteAllBytesAsync(imagePath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var policyPath = Path.Combine(tempRoot, "workspace-policies.json");
        var workspaceId = "workspace-image";
        var policyStore = new JsonWorkspacePolicyStore("OpenPlane", policyPath);
        await policyStore.SaveAsync(
            new WorkspacePolicy(
                workspaceId,
                [new PathGrant(grantedRoot, AllowRead: true, AllowWrite: false, AllowCreate: false)],
                new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase))),
            CancellationToken.None);

        var startInfo = new ProcessStartInfo("dotnet", $"\"{hostDllPath}\"")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["OPENPLANE_WORKSPACE_POLICIES_PATH"] = policyPath;

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        try
        {
            var request = new WorkerRequest(
                Guid.NewGuid().ToString("N"),
                WorkerRequestType.ExecuteStep,
                workspaceId,
                new PlanStep(Guid.NewGuid().ToString("N"), "Read image", $"tool:read|{imagePath}", SignificantAction: false),
                DateTimeOffset.UtcNow);

            await process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
            await process.StandardInput.FlushAsync();

            var responseLine = await process.StandardOutput.ReadLineAsync();
            Assert.False(string.IsNullOrWhiteSpace(responseLine));

            var response = JsonSerializer.Deserialize<WorkerResponse>(responseLine!, JsonOptions);
            Assert.NotNull(response);
            Assert.True(response!.Success, response.ErrorMessage);
            Assert.Contains("[extract-only:.png]", response.Output ?? string.Empty);
        }
        finally
        {
            try
            {
                if (!process!.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
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

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }

    private static async Task EnsureAgentHostBuiltAsync(string hostProjectPath)
    {
        var build = new ProcessStartInfo("dotnet", $"build \"{hostProjectPath}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(build);
        Assert.NotNull(process);
        await process!.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Failed to build AgentHost. stdout: {stdout}\nstderr: {stderr}");
        }
    }
}
