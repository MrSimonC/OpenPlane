using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class InlineAgentExecutor : IAgentExecutor
{
    public Task<string> ExecuteStepAsync(PlanStep step, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"Executed: {step.Title}");
    }
}
