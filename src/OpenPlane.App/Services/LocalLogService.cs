using System.Text.Json;

namespace OpenPlane.App.Services;

public interface ILocalLogService
{
    Task LogAsync(string category, string message, CancellationToken cancellationToken);
    Task<string?> ExportAsync(string destinationDirectory, CancellationToken cancellationToken);
}

public sealed class LocalLogService : ILocalLogService
{
    private readonly bool enabled;
    private readonly string logPath;

    public LocalLogService(string appName)
    {
        enabled = string.Equals(Environment.GetEnvironmentVariable("OPENPLANE_ENABLE_LOCAL_LOGS"), "1", StringComparison.OrdinalIgnoreCase);
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "logs");
        Directory.CreateDirectory(dir);
        logPath = Path.Combine(dir, "openplane.log.jsonl");
    }

    public async Task LogAsync(string category, string message, CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            return;
        }

        var entry = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            category,
            message
        };

        var json = JsonSerializer.Serialize(entry);
        await File.AppendAllTextAsync(logPath, json + Environment.NewLine, cancellationToken);
    }

    public async Task<string?> ExportAsync(string destinationDirectory, CancellationToken cancellationToken)
    {
        if (!enabled || !File.Exists(logPath))
        {
            return null;
        }

        Directory.CreateDirectory(destinationDirectory);
        var destinationPath = Path.Combine(destinationDirectory, $"openplane-log-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.jsonl");
        var contents = await File.ReadAllTextAsync(logPath, cancellationToken);
        await File.WriteAllTextAsync(destinationPath, contents, cancellationToken);
        return destinationPath;
    }
}
