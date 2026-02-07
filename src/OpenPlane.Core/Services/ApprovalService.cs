using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class ApprovalService : IApprovalService
{
    public Task<ExecutionPlan> ApproveAsync(ExecutionPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(plan with { IsApproved = true });
    }

    public bool IsApproved(ExecutionPlan plan)
    {
        return plan.IsApproved;
    }
}
