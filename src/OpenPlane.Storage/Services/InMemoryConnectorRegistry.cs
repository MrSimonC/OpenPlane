using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Storage.Services;

public sealed class InMemoryConnectorRegistry : IConnectorRegistry
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private readonly Dictionary<string, ConnectorDefinition> connectors = new(StringComparer.OrdinalIgnoreCase);

    public async Task SaveAsync(ConnectorDefinition connector, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            connectors[connector.Name] = connector;
        }
        finally
        {
            mutex.Release();
        }
    }

    public async Task<IReadOnlyList<ConnectorDefinition>> GetAllAsync(CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            return connectors.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally
        {
            mutex.Release();
        }
    }
}
