using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.IntegrationTests.Flows;

public sealed class PlanApprovalFlowTests
{
    [Fact]
    public async Task ApprovedPlan_ProducesTerminalRunCompleted()
    {
        var planner = new PlannerService();
        var approval = new ApprovalService();
        var orchestrator = new RunOrchestrator(
            new InlineAgentExecutor(new FileToolService(new AccessPolicyService()), new InMemoryWorkspacePolicyStore()),
            approval);

        var plan = await planner.CreatePlanAsync("Create and validate changes", CancellationToken.None);
        var approved = await approval.ApproveAsync(plan, CancellationToken.None);

        RunEvent? lastEvent = null;
        await foreach (var runEvent in orchestrator.ExecuteAsync(approved, CancellationToken.None))
        {
            lastEvent = runEvent;
        }

        Assert.NotNull(lastEvent);
        Assert.Equal(RunEventType.RunCompleted, lastEvent!.EventType);
    }

    private sealed class InMemoryWorkspacePolicyStore : OpenPlane.Core.Abstractions.IWorkspacePolicyStore
    {
        public Task<WorkspacePolicy> GetAsync(string workspaceId, CancellationToken cancellationToken)
        {
            var root = Path.GetTempPath();
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
