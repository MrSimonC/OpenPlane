using System.Text.Json;
using OpenPlane.Core.Abstractions;

namespace OpenPlane.Storage.Services;

public sealed class JsonModelSelectionStore(string appName) : IModelSelectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "model-selections.json");

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(IReadOnlyDictionary<string, string> selections, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (directory is null)
        {
            throw new InvalidOperationException("Settings path directory is invalid.");
        }

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(selections, JsonOptions);
        await File.WriteAllTextAsync(settingsPath, json, cancellationToken);
    }
}
