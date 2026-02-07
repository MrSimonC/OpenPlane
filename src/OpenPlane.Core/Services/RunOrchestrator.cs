using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class RunOrchestrator(IAgentExecutor agentExecutor, IApprovalService approvalService) : IRunOrchestrator
{
    public async IAsyncEnumerable<RunEvent> ExecuteAsync(ExecutionPlan plan, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!approvalService.IsApproved(plan))
        {
            throw new InvalidOperationException("Plan must be approved before execution.");
        }

        var runId = Guid.NewGuid().ToString("N");
        yield return new RunEvent(runId, RunEventType.RunStarted, "Run started.", DateTimeOffset.UtcNow);

        foreach (var step in plan.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new RunEvent(runId, RunEventType.StepStarted, step.Title, DateTimeOffset.UtcNow, step.Id);

            string output;
            RunEvent? failedEvent = null;
            RunEvent? policyViolationEvent = null;
            try
            {
                output = await agentExecutor.ExecuteStepAsync(step, cancellationToken);
            }
            catch (PolicyViolationException ex)
            {
                policyViolationEvent = new RunEvent(runId, RunEventType.PolicyViolation, ex.Message, DateTimeOffset.UtcNow, step.Id);
                failedEvent = new RunEvent(runId, RunEventType.RunFailed, "Run failed due to policy violation.", DateTimeOffset.UtcNow, step.Id);
                output = string.Empty;
            }
            catch (Exception ex)
            {
                failedEvent = new RunEvent(runId, RunEventType.RunFailed, ex.Message, DateTimeOffset.UtcNow, step.Id);
                output = string.Empty;
            }

            if (policyViolationEvent is not null)
            {
                yield return policyViolationEvent;
            }

            if (failedEvent is not null)
            {
                yield return failedEvent;
                yield break;
            }

            yield return new RunEvent(runId, RunEventType.StepOutput, output, DateTimeOffset.UtcNow, step.Id);
            yield return new RunEvent(runId, RunEventType.StepCompleted, step.Title, DateTimeOffset.UtcNow, step.Id);
        }

        yield return new RunEvent(runId, RunEventType.RunCompleted, "Run completed.", DateTimeOffset.UtcNow);
    }
}
