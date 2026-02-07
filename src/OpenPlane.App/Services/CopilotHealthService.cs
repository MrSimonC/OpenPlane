using System.Diagnostics;
using GitHub.Copilot.SDK;

namespace OpenPlane.App.Services;

public sealed record CopilotHealthReport(
    string ExecutionMode,
    string EffectiveCommand,
    string? CliVersion,
    bool ModelProbeSucceeded,
    string? LastStartupError);

public interface ICopilotHealthService
{
    Task<CopilotHealthReport> CheckAsync(CancellationToken cancellationToken);
}

public sealed class CopilotHealthService(
    ICopilotClientOptionsFactory optionsFactory,
    ICopilotExecutionSettingsStore settingsStore) : ICopilotHealthService
{
    public async Task<CopilotHealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        var executionMode = "Embedded CLI process";
        var command = optionsFactory.DisplayCommand;

        string? cliVersion = null;
        string? startupError = null;

        (cliVersion, startupError) = await TryGetCliVersionAsync(cancellationToken);

        var modelProbeSucceeded = false;
        try
        {
            await using var client = new CopilotClient(optionsFactory.Create(settings));
            await client.StartAsync();
            var models = await client.ListModelsAsync();
            modelProbeSucceeded = models.Count > 0;
            if (!modelProbeSucceeded)
            {
                startupError ??= "Model probe returned no models.";
            }
        }
        catch (Exception ex)
        {
            startupError ??= ex.Message;
        }

        return new CopilotHealthReport(executionMode, command, cliVersion, modelProbeSucceeded, startupError);
    }

    private async Task<(string? Version, string? Error)> TryGetCliVersionAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(optionsFactory.ResolvedCliPath))
        {
            return (null, "Bundled Copilot CLI not found in app output.");
        }

        var args = string.Join(' ', optionsFactory.ResolvedCliArgs.Append("--version"));
        var startInfo = new ProcessStartInfo(optionsFactory.ResolvedCliPath, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (null, "Failed to start CLI version check process.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = (await stdoutTask).Trim();
            var stderr = (await stderrTask).Trim();

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return (null, string.IsNullOrWhiteSpace(error) ? "CLI version check failed." : error);
            }

            return (string.IsNullOrWhiteSpace(stdout) ? null : stdout, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
