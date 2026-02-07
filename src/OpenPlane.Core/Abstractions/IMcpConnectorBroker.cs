using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IMcpConnectorBroker
{
    Task<ConnectorStatus> ConnectAsync(ConnectorDefinition definition, CancellationToken cancellationToken);
    Task DisconnectAsync(string connectorName, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectorStatus>> GetStatusesAsync(CancellationToken cancellationToken);
}
