using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class PlanExecutionService(
    IPlannerService plannerService,
    IApprovalService approvalService,
    IAgentExecutor agentExecutor,
    IExecutionPlanStore executionPlanStore,
    IRunStateStore runStateStore) : IPlanExecutionService
{
    public Task<int> RecoverRunningRunsAsync(CancellationToken cancellationToken)
    {
        return runStateStore.RecoverRunningSessionsAsync(cancellationToken);
    }

    public async Task<ExecutionPlan> CreatePlanAsync(string workspaceId, string prompt, CancellationToken cancellationToken)
    {
        var plan = await plannerService.CreatePlanAsync(prompt, cancellationToken);
        await executionPlanStore.SaveLatestAsync(workspaceId, plan, cancellationToken);
        return plan;
    }

    public Task<ExecutionPlan?> GetLatestPlanAsync(string workspaceId, CancellationToken cancellationToken)
    {
        return executionPlanStore.GetLatestAsync(workspaceId, cancellationToken);
    }

    public async Task<ExecutionPlan> ApproveLatestPlanAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var plan = await executionPlanStore.GetLatestAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("No plan available to approve.");

        var approved = await approvalService.ApproveAsync(plan, cancellationToken);
        await executionPlanStore.SaveLatestAsync(workspaceId, approved, cancellationToken);
        return approved;
    }

    public async IAsyncEnumerable<RunEvent> ExecuteLatestApprovedPlanAsync(
        string workspaceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = await executionPlanStore.GetLatestAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("No plan available.");

        if (!approvalService.IsApproved(plan))
        {
            throw new InvalidOperationException("Latest plan must be approved before execution.");
        }

        var runId = Guid.NewGuid().ToString("N");
        var session = new RunSession(
            runId,
            workspaceId,
            plan.PlanId,
            RunStatus.Pending,
            NextStepIndex: 0,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            CompletedAtUtc: null,
            FailureReason: null);

        await runStateStore.SaveSessionAsync(session, cancellationToken);

        var stepStates = plan.Steps
            .Select(step => new RunStepState(runId, step.Id, step.Title, RunStatus.Pending, null, DateTimeOffset.UtcNow))
            .ToArray();
        await runStateStore.SaveStepStatesAsync(runId, stepStates, cancellationToken);

        await foreach (var evt in ExecuteFromIndexAsync(plan, session, stepStates, startIndex: 0, cancellationToken))
        {
            yield return evt;
        }
    }

    public async IAsyncEnumerable<RunEvent> ResumeLatestRunAsync(
        string workspaceId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var session = await runStateStore.GetLatestSessionAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("No previous run session found.");

        if (session.Status == RunStatus.Completed)
        {
            throw new InvalidOperationException("Latest run is already completed.");
        }

        var plan = await executionPlanStore.GetLatestAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("No plan available for resume.");

        var stepStates = await runStateStore.GetStepStatesAsync(session.RunId, cancellationToken);
        if (stepStates.Count == 0)
        {
            stepStates = plan.Steps
                .Select(step => new RunStepState(session.RunId, step.Id, step.Title, RunStatus.Pending, null, DateTimeOffset.UtcNow))
                .ToArray();
            await runStateStore.SaveStepStatesAsync(session.RunId, stepStates, cancellationToken);
        }

        await foreach (var evt in ExecuteFromIndexAsync(plan, session, stepStates, session.NextStepIndex, cancellationToken))
        {
            yield return evt;
        }
    }

    public Task<RunSession?> GetLatestRunAsync(string workspaceId, CancellationToken cancellationToken)
    {
        return runStateStore.GetLatestSessionAsync(workspaceId, cancellationToken);
    }

    public Task<IReadOnlyList<RunStepState>> GetRunStepStatesAsync(string runId, CancellationToken cancellationToken)
    {
        return runStateStore.GetStepStatesAsync(runId, cancellationToken);
    }

    private async IAsyncEnumerable<RunEvent> ExecuteFromIndexAsync(
        ExecutionPlan plan,
        RunSession session,
        IReadOnlyList<RunStepState> existingStates,
        int startIndex,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var mutableStates = existingStates.ToDictionary(x => x.StepId, x => x);

        session = session with { Status = RunStatus.Running, FailureReason = null, CompletedAtUtc = null };
        await runStateStore.SaveSessionAsync(session, cancellationToken);
        yield return new RunEvent(session.RunId, RunEventType.RunStarted, $"Run started from step {startIndex + 1}.", DateTimeOffset.UtcNow);

        for (var i = Math.Max(0, startIndex); i < plan.Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var step = plan.Steps[i];
            var startedState = new RunStepState(session.RunId, step.Id, step.Title, RunStatus.Running, null, DateTimeOffset.UtcNow);
            mutableStates[step.Id] = startedState;
            await runStateStore.SaveStepStatesAsync(session.RunId, mutableStates.Values.ToArray(), cancellationToken);
            yield return new RunEvent(session.RunId, RunEventType.StepStarted, step.Title, DateTimeOffset.UtcNow, step.Id);

            string? output = null;
            RunEvent? policyViolationEvent = null;
            RunEvent? runFailedEvent = null;
            var stepCompleted = false;
            try
            {
                output = await agentExecutor.ExecuteStepAsync(step, cancellationToken);
                var completedState = startedState with { Status = RunStatus.Completed, Output = output, UpdatedAtUtc = DateTimeOffset.UtcNow };
                mutableStates[step.Id] = completedState;
                await runStateStore.SaveStepStatesAsync(session.RunId, mutableStates.Values.ToArray(), cancellationToken);
                session = session with { NextStepIndex = i + 1 };
                await runStateStore.SaveSessionAsync(session, cancellationToken);
                stepCompleted = true;
            }
            catch (PolicyViolationException ex)
            {
                var failedState = startedState with { Status = RunStatus.Failed, Output = ex.Message, UpdatedAtUtc = DateTimeOffset.UtcNow };
                mutableStates[step.Id] = failedState;
                await runStateStore.SaveStepStatesAsync(session.RunId, mutableStates.Values.ToArray(), cancellationToken);

                session = session with
                {
                    Status = RunStatus.Failed,
                    NextStepIndex = i,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    FailureReason = ex.Message
                };

                await runStateStore.SaveSessionAsync(session, cancellationToken);
                policyViolationEvent = new RunEvent(session.RunId, RunEventType.PolicyViolation, ex.Message, DateTimeOffset.UtcNow, step.Id);
                runFailedEvent = new RunEvent(session.RunId, RunEventType.RunFailed, "Run failed due to policy violation.", DateTimeOffset.UtcNow, step.Id);
            }
            catch (Exception ex)
            {
                var failedState = startedState with { Status = RunStatus.Failed, Output = ex.Message, UpdatedAtUtc = DateTimeOffset.UtcNow };
                mutableStates[step.Id] = failedState;
                await runStateStore.SaveStepStatesAsync(session.RunId, mutableStates.Values.ToArray(), cancellationToken);

                session = session with
                {
                    Status = RunStatus.Failed,
                    NextStepIndex = i,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    FailureReason = ex.Message
                };

                await runStateStore.SaveSessionAsync(session, cancellationToken);
                runFailedEvent = new RunEvent(session.RunId, RunEventType.RunFailed, ex.Message, DateTimeOffset.UtcNow, step.Id);
            }

            if (stepCompleted)
            {
                yield return new RunEvent(session.RunId, RunEventType.StepOutput, output ?? string.Empty, DateTimeOffset.UtcNow, step.Id);
                yield return new RunEvent(session.RunId, RunEventType.StepCompleted, step.Title, DateTimeOffset.UtcNow, step.Id);
                continue;
            }

            if (policyViolationEvent is not null)
            {
                yield return policyViolationEvent;
            }

            if (runFailedEvent is not null)
            {
                yield return runFailedEvent;
                yield break;
            }
        }

        session = session with
        {
            Status = RunStatus.Completed,
            NextStepIndex = plan.Steps.Count,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            FailureReason = null
        };

        await runStateStore.SaveSessionAsync(session, cancellationToken);
        yield return new RunEvent(session.RunId, RunEventType.RunCompleted, "Run completed.", DateTimeOffset.UtcNow);
    }
}
