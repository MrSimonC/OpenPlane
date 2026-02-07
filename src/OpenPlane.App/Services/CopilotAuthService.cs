using System.Diagnostics;
using System.Text.RegularExpressions;
using GitHub.Copilot.SDK;

namespace OpenPlane.App.Services;

public sealed record CopilotAuthState(bool IsAuthenticated, string? Login, string? Host, string? StatusMessage);
public sealed record CopilotLoginResult(
    bool Succeeded,
    string Message,
    string? DeviceCode,
    string? VerificationUrl,
    string? ManualCommandGuidance);

public interface ICopilotAuthService
{
    Task<CopilotAuthState> GetAuthStateAsync(CancellationToken cancellationToken);
    Task<CopilotLoginResult> LoginAsync(CancellationToken cancellationToken);
}

public sealed class CopilotAuthService : ICopilotAuthService
{
    private readonly ICopilotClientOptionsFactory optionsFactory;
    private readonly ICopilotExecutionSettingsStore settingsStore;

    public CopilotAuthService(ICopilotClientOptionsFactory optionsFactory, ICopilotExecutionSettingsStore settingsStore)
    {
        this.optionsFactory = optionsFactory;
        this.settingsStore = settingsStore;
    }

    public async Task<CopilotAuthState> GetAuthStateAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);

        try
        {
            await using var client = new CopilotClient(optionsFactory.Create(settings));

            await client.StartAsync();
            var status = await client.GetAuthStatusAsync();
            return new CopilotAuthState(status.IsAuthenticated, status.Login, status.Host, status.StatusMessage);
        }
        catch (Exception ex)
        {
            var location = settings.Mode == CopilotExecutionMode.ExternalCopilotEndpoint
                ? settings.ExternalCliUrl ?? "(missing external endpoint URL)"
                : optionsFactory.DisplayCommand;
            return new CopilotAuthState(false, null, null, $"{ex.Message} ({location})");
        }
    }

    public async Task<CopilotLoginResult> LoginAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        if (settings.Mode == CopilotExecutionMode.ExternalCopilotEndpoint)
        {
            return new CopilotLoginResult(
                false,
                "Login is unavailable in External Copilot endpoint mode. Authenticate against the external endpoint process directly.",
                null,
                null,
                null);
        }

        if (string.IsNullOrWhiteSpace(optionsFactory.ResolvedCliPath))
        {
            return new CopilotLoginResult(
                false,
                "Bundled Copilot CLI not found in app output. Build did not include runtimes/<rid>/native/copilot.",
                null,
                null,
                null);
        }

        var manualHint = $"Run in Terminal if needed: {optionsFactory.DisplayCommand} login";

        var invocationArgs = string.Join(' ', optionsFactory.ResolvedCliArgs.Append("login"));

        var processStartInfo = new ProcessStartInfo(optionsFactory.ResolvedCliPath, invocationArgs)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start `copilot login`.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;
        var message = string.Join(Environment.NewLine, new[] { output, error }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var parsed = ParseDeviceFlow(message);
        var finalMessage = string.IsNullOrWhiteSpace(message) ? "Copilot login completed." : message.Trim();

        if (process.ExitCode != 0)
        {
            var failure = string.IsNullOrWhiteSpace(message) ? "Copilot login failed." : finalMessage;
            return new CopilotLoginResult(false, failure, parsed.DeviceCode, parsed.VerificationUrl, manualHint);
        }

        return new CopilotLoginResult(
            true,
            finalMessage,
            parsed.DeviceCode,
            parsed.VerificationUrl,
            parsed.DeviceCode is null ? manualHint : null);
    }

    private static (string? DeviceCode, string? VerificationUrl) ParseDeviceFlow(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return (null, null);
        }

        var clean = Regex.Replace(rawOutput, @"\x1B\[[0-9;]*[A-Za-z]", string.Empty);

        var codeMatch = Regex.Match(clean, @"\b[A-Z0-9]{4}(?:-[A-Z0-9]{4})+\b", RegexOptions.IgnoreCase);
        var urlMatch = Regex.Match(clean, @"https?://\S+", RegexOptions.IgnoreCase);

        var deviceCode = codeMatch.Success ? codeMatch.Value.ToUpperInvariant() : null;
        var verificationUrl = urlMatch.Success ? urlMatch.Value.TrimEnd('.', ',', ';') : null;

        return (deviceCode, verificationUrl);
    }
}
