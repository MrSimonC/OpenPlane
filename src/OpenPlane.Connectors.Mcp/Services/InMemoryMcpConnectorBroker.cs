using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Connectors.Mcp.Services;

public sealed class InMemoryMcpConnectorBroker : IMcpConnectorBroker
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private readonly Dictionary<string, ConnectorStatus> statuses = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ConnectorStatus> ConnectAsync(ConnectorDefinition definition, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var status = new ConnectorStatus(definition.Name, Connected: true, LastError: null);
            statuses[definition.Name] = status;
            return status;
        }
        finally
        {
            mutex.Release();
        }
    }

    public async Task DisconnectAsync(string connectorName, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            if (statuses.ContainsKey(connectorName))
            {
                statuses[connectorName] = new ConnectorStatus(connectorName, Connected: false, LastError: null);
            }
        }
        finally
        {
            mutex.Release();
        }
    }

    public async Task<IReadOnlyList<ConnectorStatus>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            return statuses.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally
        {
            mutex.Release();
        }
    }
}
