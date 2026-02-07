using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IWorkspacePolicyStore
{
    Task<WorkspacePolicy> GetAsync(string workspaceId, CancellationToken cancellationToken);
    Task SaveAsync(WorkspacePolicy policy, CancellationToken cancellationToken);
}
