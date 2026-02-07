using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IPlannerService
{
    Task<ExecutionPlan> CreatePlanAsync(string prompt, CancellationToken cancellationToken);
}
