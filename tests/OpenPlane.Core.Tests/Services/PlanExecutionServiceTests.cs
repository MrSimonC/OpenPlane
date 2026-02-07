using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class PlanExecutionServiceTests
{
    [Fact]
    public async Task CreateApproveExecute_CompletesRunAndPersistsState()
    {
        var planStore = new InMemoryPlanStore();
        var runStore = new InMemoryRunStateStore();
        var service = new PlanExecutionService(
            new PlannerService(),
            new ApprovalService(),
            new SuccessExecutor(),
            planStore,
            runStore);

        var workspaceId = "ws-1";
        await service.CreatePlanAsync(workspaceId, "summarize", CancellationToken.None);
        await service.ApproveLatestPlanAsync(workspaceId, CancellationToken.None);

        var events = new List<RunEvent>();
        await foreach (var evt in service.ExecuteLatestApprovedPlanAsync(workspaceId, CancellationToken.None))
        {
            events.Add(evt);
        }

        var latest = await service.GetLatestRunAsync(workspaceId, CancellationToken.None);
        Assert.NotNull(latest);
        Assert.Equal(RunStatus.Completed, latest!.Status);
        Assert.Contains(events, x => x.EventType == RunEventType.RunCompleted);
    }

    [Fact]
    public async Task ExecuteWithoutApproval_Throws()
    {
        var service = new PlanExecutionService(
            new PlannerService(),
            new ApprovalService(),
            new SuccessExecutor(),
            new InMemoryPlanStore(),
            new InMemoryRunStateStore());

        var workspaceId = "ws-2";
        await service.CreatePlanAsync(workspaceId, "prompt", CancellationToken.None);

        var enumerator = service.ExecuteLatestApprovedPlanAsync(workspaceId, CancellationToken.None).GetAsyncEnumerator();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => enumerator.MoveNextAsync().AsTask());
        Assert.Contains("approved", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResumeLatestRun_ContinuesFromFailedStep()
    {
        var planStore = new InMemoryPlanStore();
        var runStore = new InMemoryRunStateStore();
        var flakyExecutor = new FailOnceExecutor();
        var service = new PlanExecutionService(
            new PlannerService(),
            new ApprovalService(),
            flakyExecutor,
            planStore,
            runStore);

        var workspaceId = "ws-3";
        await service.CreatePlanAsync(workspaceId, "tool:create-file|/tmp/a|b", CancellationToken.None);
        await service.ApproveLatestPlanAsync(workspaceId, CancellationToken.None);

        var firstPassEvents = new List<RunEvent>();
        await foreach (var evt in service.ExecuteLatestApprovedPlanAsync(workspaceId, CancellationToken.None))
        {
            firstPassEvents.Add(evt);
        }

        Assert.Contains(firstPassEvents, x => x.EventType == RunEventType.RunFailed);

        var resumeEvents = new List<RunEvent>();
        await foreach (var evt in service.ResumeLatestRunAsync(workspaceId, CancellationToken.None))
        {
            resumeEvents.Add(evt);
        }

        Assert.Contains(resumeEvents, x => x.EventType == RunEventType.RunCompleted);
        var latest = await service.GetLatestRunAsync(workspaceId, CancellationToken.None);
        Assert.NotNull(latest);
        Assert.Equal(RunStatus.Completed, latest!.Status);
    }

    private sealed class SuccessExecutor : IAgentExecutor
    {
        public Task<string> ExecuteStepAsync(PlanStep step, CancellationToken cancellationToken)
        {
            return Task.FromResult($"ok:{step.Title}");
        }
    }

    private sealed class FailOnceExecutor : IAgentExecutor
    {
        private bool failed;

        public Task<string> ExecuteStepAsync(PlanStep step, CancellationToken cancellationToken)
        {
            if (!failed)
            {
                failed = true;
                throw new InvalidOperationException("simulated failure");
            }

            return Task.FromResult($"ok:{step.Title}");
        }
    }

    private sealed class InMemoryPlanStore : IExecutionPlanStore
    {
        private readonly Dictionary<string, ExecutionPlan> values = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveLatestAsync(string workspaceId, ExecutionPlan plan, CancellationToken cancellationToken)
        {
            values[workspaceId] = plan;
            return Task.CompletedTask;
        }

        public Task<ExecutionPlan?> GetLatestAsync(string workspaceId, CancellationToken cancellationToken)
        {
            values.TryGetValue(workspaceId, out var plan);
            return Task.FromResult(plan);
        }
    }

    private sealed class InMemoryRunStateStore : IRunStateStore
    {
        private readonly Dictionary<string, RunSession> sessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RunStepState[]> steps = new(StringComparer.OrdinalIgnoreCase);

        public Task SaveSessionAsync(RunSession session, CancellationToken cancellationToken)
        {
            sessions[session.RunId] = session;
            return Task.CompletedTask;
        }

        public Task<RunSession?> GetLatestSessionAsync(string workspaceId, CancellationToken cancellationToken)
        {
            var latest = sessions.Values
                .Where(x => string.Equals(x.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefault();
            return Task.FromResult(latest);
        }

        public Task<RunSession?> GetSessionAsync(string runId, CancellationToken cancellationToken)
        {
            sessions.TryGetValue(runId, out var session);
            return Task.FromResult(session);
        }

        public Task<int> RecoverRunningSessionsAsync(CancellationToken cancellationToken)
        {
            var recovered = 0;
            foreach (var (runId, session) in sessions.ToArray())
            {
                if (session.Status != RunStatus.Running)
                {
                    continue;
                }

                sessions[runId] = session with
                {
                    Status = RunStatus.Failed,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    FailureReason = "Recovered after unexpected shutdown."
                };
                recovered++;
            }

            return Task.FromResult(recovered);
        }

        public Task SaveStepStatesAsync(string runId, IReadOnlyList<RunStepState> stepStates, CancellationToken cancellationToken)
        {
            steps[runId] = stepStates.ToArray();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RunStepState>> GetStepStatesAsync(string runId, CancellationToken cancellationToken)
        {
            if (steps.TryGetValue(runId, out var value))
            {
                return Task.FromResult<IReadOnlyList<RunStepState>>(value);
            }

            return Task.FromResult<IReadOnlyList<RunStepState>>([]);
        }
    }
}
