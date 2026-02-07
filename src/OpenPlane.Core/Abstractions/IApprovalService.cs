using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IApprovalService
{
    Task<ExecutionPlan> ApproveAsync(ExecutionPlan plan, CancellationToken cancellationToken);
    bool IsApproved(ExecutionPlan plan);
}
