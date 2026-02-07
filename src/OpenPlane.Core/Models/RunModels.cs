namespace OpenPlane.Core.Models;

public enum RunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum RunEventType
{
    RunStarted,
    StepStarted,
    StepOutput,
    StepCompleted,
    PolicyViolation,
    RunCompleted,
    RunFailed
}

public sealed record RunSession(
    string RunId,
    string WorkspaceId,
    string PlanId,
    RunStatus Status,
    int NextStepIndex,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FailureReason);

public sealed record RunEvent(
    string RunId,
    RunEventType EventType,
    string Message,
    DateTimeOffset CreatedAtUtc,
    string? StepId = null);
