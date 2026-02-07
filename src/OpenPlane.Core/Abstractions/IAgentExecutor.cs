using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IAgentExecutor
{
    Task<string> ExecuteStepAsync(PlanStep step, CancellationToken cancellationToken);
}
