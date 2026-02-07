using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Storage.Services;

public sealed class JsonConnectorRegistry(string appName) : IConnectorRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "connectors.json");

    public async Task SaveAsync(ConnectorDefinition connector, CancellationToken cancellationToken)
    {
        var all = (await LoadAsync(cancellationToken)).ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        all[connector.Name] = Normalize(connector);
        await SaveAllAsync(all.Values.ToArray(), cancellationToken);
    }

    public Task<IReadOnlyList<ConnectorDefinition>> GetAllAsync(CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ConnectorDefinition>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var items = JsonSerializer.Deserialize<List<ConnectorDefinition>>(json, JsonOptions) ?? [];
        return items
            .Select(Normalize)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task SaveAllAsync(IReadOnlyList<ConnectorDefinition> connectors, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException("Connector registry path is invalid.");
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(connectors, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static ConnectorDefinition Normalize(ConnectorDefinition connector)
    {
        var env = new Dictionary<string, string>(connector.EnvironmentVariables, StringComparer.OrdinalIgnoreCase);
        var scopes = connector.AllowedScopes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return connector with
        {
            Name = connector.Name.Trim(),
            Command = connector.Command.Trim(),
            EnvironmentVariables = env,
            AllowedScopes = scopes
        };
    }
}
