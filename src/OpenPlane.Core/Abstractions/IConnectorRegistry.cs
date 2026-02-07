using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IConnectorRegistry
{
    Task SaveAsync(ConnectorDefinition connector, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConnectorDefinition>> GetAllAsync(CancellationToken cancellationToken);
}
