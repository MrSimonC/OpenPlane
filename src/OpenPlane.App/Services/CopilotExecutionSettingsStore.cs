using System.Text.Json;

namespace OpenPlane.App.Services;

public enum CopilotExecutionMode
{
    EmbeddedCliProcess,
    ExternalCopilotEndpoint
}

public sealed record CopilotExecutionSettings(
    CopilotExecutionMode Mode,
    string? ExternalCliUrl)
{
    public static CopilotExecutionSettings Default { get; } = new(CopilotExecutionMode.EmbeddedCliProcess, null);
}

public interface ICopilotExecutionSettingsStore
{
    Task<CopilotExecutionSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CopilotExecutionSettings settings, CancellationToken cancellationToken);
}

public sealed class JsonCopilotExecutionSettingsStore : ICopilotExecutionSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string settingsPath;

    public JsonCopilotExecutionSettingsStore(string appName)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        Directory.CreateDirectory(directory);
        settingsPath = Path.Combine(directory, "execution-settings.json");
    }

    public async Task<CopilotExecutionSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return CopilotExecutionSettings.Default;
        }

        var json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return CopilotExecutionSettings.Default;
        }

        var deserialized = JsonSerializer.Deserialize<CopilotExecutionSettings>(json, JsonOptions);
        return deserialized ?? CopilotExecutionSettings.Default;
    }

    public async Task SaveAsync(CopilotExecutionSettings settings, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(settingsPath, json, cancellationToken);
    }
}
