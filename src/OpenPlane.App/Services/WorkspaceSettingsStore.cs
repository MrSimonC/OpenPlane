using System.Text.Json;

namespace OpenPlane.App.Services;

public sealed record WorkspaceSettings(
    IReadOnlyList<string> WorkspaceIds,
    string SelectedWorkspaceId)
{
    public static WorkspaceSettings Default { get; } = new(["default"], "default");
}

public interface IWorkspaceSettingsStore
{
    Task<WorkspaceSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(WorkspaceSettings settings, CancellationToken cancellationToken);
}

public sealed class JsonWorkspaceSettingsStore : IWorkspaceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string settingsPath;

    public JsonWorkspaceSettingsStore(string appName)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        Directory.CreateDirectory(directory);
        settingsPath = Path.Combine(directory, "workspaces.json");
    }

    public async Task<WorkspaceSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return WorkspaceSettings.Default;
        }

        var json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return WorkspaceSettings.Default;
        }

        var loaded = JsonSerializer.Deserialize<WorkspaceSettings>(json, JsonOptions);
        return Normalize(loaded ?? WorkspaceSettings.Default);
    }

    public async Task SaveAsync(WorkspaceSettings settings, CancellationToken cancellationToken)
    {
        var normalized = Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        await File.WriteAllTextAsync(settingsPath, json, cancellationToken);
    }

    private static WorkspaceSettings Normalize(WorkspaceSettings settings)
    {
        var workspaceIds = settings.WorkspaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (workspaceIds.Count == 0)
        {
            workspaceIds.Add("default");
        }

        var selected = workspaceIds.FirstOrDefault(id => string.Equals(id, settings.SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase))
            ?? workspaceIds[0];

        return new WorkspaceSettings(workspaceIds, selected);
    }
}
