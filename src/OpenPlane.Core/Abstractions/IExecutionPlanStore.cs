using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IExecutionPlanStore
{
    Task SaveLatestAsync(string workspaceId, ExecutionPlan plan, CancellationToken cancellationToken);
    Task<ExecutionPlan?> GetLatestAsync(string workspaceId, CancellationToken cancellationToken);
}
