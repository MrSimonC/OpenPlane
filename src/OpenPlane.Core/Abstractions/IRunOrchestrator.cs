using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IRunOrchestrator
{
    IAsyncEnumerable<RunEvent> ExecuteAsync(ExecutionPlan plan, CancellationToken cancellationToken);
}
