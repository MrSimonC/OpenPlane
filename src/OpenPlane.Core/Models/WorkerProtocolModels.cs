namespace OpenPlane.Core.Models;

public enum WorkerRequestType
{
    Ping,
    ExecuteStep
}

public sealed record WorkerRequest(
    string RequestId,
    WorkerRequestType Type,
    string? WorkspaceId,
    PlanStep? Step,
    DateTimeOffset CreatedAtUtc);

public sealed record WorkerResponse(
    string RequestId,
    bool Success,
    string? Output,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAtUtc);
