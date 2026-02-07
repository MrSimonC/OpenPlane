using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class RunOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_ThrowsWhenPlanIsNotApproved()
    {
        var orchestrator = new RunOrchestrator(new InlineAgentExecutor(), new ApprovalService());
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
        var orchestrator = new RunOrchestrator(new InlineAgentExecutor(), approvalService);
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
}
