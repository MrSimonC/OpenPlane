namespace OpenPlane.Core.Models;

public enum PlanRiskLevel
{
    Low,
    Medium,
    High
}

public sealed record PlanStep(string Id, string Title, string Details, bool SignificantAction);

public sealed record ExecutionPlan(
    string PlanId,
    string Prompt,
    IReadOnlyList<PlanStep> Steps,
    PlanRiskLevel RiskLevel,
    DateTimeOffset CreatedAtUtc,
    bool IsApproved = false);
