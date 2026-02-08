using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class PlannerService : IPlannerService
{
    public Task<ExecutionPlan> CreatePlanAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var steps = BuildPromptAwareSteps(prompt);

        var plan = new ExecutionPlan(
            Guid.NewGuid().ToString("N"),
            prompt,
            steps,
            PlanRiskLevel.Medium,
            DateTimeOffset.UtcNow);

        return Task.FromResult(plan);
    }

    private static IReadOnlyList<PlanStep> BuildPromptAwareSteps(string prompt)
    {
        var trimmed = prompt.Trim();
        var lower = trimmed.ToLowerInvariant();
        var requestContext = trimmed.Length > 3000 ? trimmed[..3000] + "\n...[truncated]" : trimmed;
        const string subagentGuidance = "Use subagents where possible to parallelize and improve reliability.";
        var steps = new List<PlanStep>
        {
            new(Guid.NewGuid().ToString("N"), "Analyze request", $"User request:\n{requestContext}\n\nIdentify intent, constraints, and expected output.\n{subagentGuidance}", false)
        };

        if (trimmed.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
        {
            steps.Add(new PlanStep(Guid.NewGuid().ToString("N"), "Execute requested tool action", trimmed, true));
        }
        else if (lower.Contains("list") && lower.Contains("file"))
        {
            steps.Add(new PlanStep(
                Guid.NewGuid().ToString("N"),
                "Search workspace files",
                "tool:search|.|*",
                true));
        }
        else
        {
            steps.Add(new PlanStep(
                Guid.NewGuid().ToString("N"),
                "Run assistant reasoning",
                $"Use Copilot to produce the requested output while staying within granted workspace policy.\n\nRequest:\n{requestContext}\n\n{subagentGuidance}",
                true));
        }

        steps.Add(new PlanStep(
            Guid.NewGuid().ToString("N"),
            "Validate and summarize",
            $"Confirm output quality and summarize final result for this request:\n{requestContext}\n\n{subagentGuidance}",
            true));

        return steps;
    }
}
