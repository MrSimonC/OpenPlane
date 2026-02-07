using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class RunOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_ThrowsWhenPlanIsNotApproved()
    {
        var orchestrator = new RunOrchestrator(new InlineAgentExecutor(new FileToolService(new AccessPolicyService()), new InMemoryWorkspacePolicyStore()), new ApprovalService());
        var plan = new ExecutionPlan(
            "plan-1",
            "prompt",
            [new PlanStep("1", "Title", "Details", SignificantAction: true)],
            PlanRiskLevel.Low,
            DateTimeOffset.UtcNow,
            IsApproved: false);

        var enumerator = orchestrator.ExecuteAsync(plan, CancellationToken.None).GetAsyncEnumerator();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await enumerator.MoveNextAsync());

        Assert.Equal("Plan must be approved before execution.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsCompletionEvents()
    {
        var approvalService = new ApprovalService();
        var orchestrator = new RunOrchestrator(new InlineAgentExecutor(new FileToolService(new AccessPolicyService()), new InMemoryWorkspacePolicyStore()), approvalService);
        var approvedPlan = await approvalService.ApproveAsync(
            new ExecutionPlan(
                "plan-1",
                "prompt",
                [new PlanStep("1", "Title", "Details", SignificantAction: true)],
                PlanRiskLevel.Low,
                DateTimeOffset.UtcNow,
                IsApproved: false),
            CancellationToken.None);

        var events = new List<RunEvent>();
        await foreach (var runEvent in orchestrator.ExecuteAsync(approvedPlan, CancellationToken.None))
        {
            events.Add(runEvent);
        }

        Assert.Contains(events, x => x.EventType == RunEventType.RunStarted);
        Assert.Contains(events, x => x.EventType == RunEventType.StepCompleted);
        Assert.Contains(events, x => x.EventType == RunEventType.RunCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsPolicyViolationEvent_WhenStepViolatesGrant()
    {
        var root = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var forbiddenFile = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"), "forbidden.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(forbiddenFile)!);

        var approvalService = new ApprovalService();
        var orchestrator = new RunOrchestrator(
            new InlineAgentExecutor(new FileToolService(new AccessPolicyService()), new InMemoryWorkspacePolicyStore(root)),
            approvalService);

        var approvedPlan = await approvalService.ApproveAsync(
            new ExecutionPlan(
                "plan-1",
                "prompt",
                [new PlanStep("1", "Create forbidden file", $"tool:create-file|{forbiddenFile}|blocked", SignificantAction: true)],
                PlanRiskLevel.Low,
                DateTimeOffset.UtcNow,
                IsApproved: false),
            CancellationToken.None);

        var events = new List<RunEvent>();
        await foreach (var runEvent in orchestrator.ExecuteAsync(approvedPlan, CancellationToken.None))
        {
            events.Add(runEvent);
        }

        Assert.Contains(events, x => x.EventType == RunEventType.PolicyViolation);
        Assert.Contains(events, x => x.EventType == RunEventType.RunFailed);
    }

    private sealed class InMemoryWorkspacePolicyStore : IWorkspacePolicyStore
    {
        private readonly string root;

        public InMemoryWorkspacePolicyStore()
        {
            root = Path.GetTempPath();
        }

        public InMemoryWorkspacePolicyStore(string root)
        {
            this.root = root;
        }

        public Task<WorkspacePolicy> GetAsync(string workspaceId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WorkspacePolicy(
                workspaceId,
                [new PathGrant(root, AllowRead: true, AllowWrite: true, AllowCreate: true)],
                new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase))));
        }

        public Task SaveAsync(WorkspacePolicy policy, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
