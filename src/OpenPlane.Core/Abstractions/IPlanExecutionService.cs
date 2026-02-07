using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IPlanExecutionService
{
    Task<ExecutionPlan> CreatePlanAsync(string workspaceId, string prompt, CancellationToken cancellationToken);
    Task<ExecutionPlan?> GetLatestPlanAsync(string workspaceId, CancellationToken cancellationToken);
    Task<ExecutionPlan> ApproveLatestPlanAsync(string workspaceId, CancellationToken cancellationToken);
    IAsyncEnumerable<RunEvent> ExecuteLatestApprovedPlanAsync(string workspaceId, CancellationToken cancellationToken);
    IAsyncEnumerable<RunEvent> ResumeLatestRunAsync(string workspaceId, CancellationToken cancellationToken);
    Task<RunSession?> GetLatestRunAsync(string workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RunStepState>> GetRunStepStatesAsync(string runId, CancellationToken cancellationToken);
}
