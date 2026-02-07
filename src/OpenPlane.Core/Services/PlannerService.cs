using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class PlannerService : IPlannerService
{
    public Task<ExecutionPlan> CreatePlanAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var steps = new List<PlanStep>
        {
            new(Guid.NewGuid().ToString("N"), "Analyze request", "Review task intent and constraints.", false),
            new(Guid.NewGuid().ToString("N"), "Apply changes", "Create or update files in granted folders only.", true),
            new(Guid.NewGuid().ToString("N"), "Validate results", "Run verification and summarize outcome.", true)
        };

        var plan = new ExecutionPlan(
            Guid.NewGuid().ToString("N"),
            prompt,
            steps,
            PlanRiskLevel.Medium,
            DateTimeOffset.UtcNow);

        return Task.FromResult(plan);
    }
}
