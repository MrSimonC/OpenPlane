namespace OpenPlane.Core.Models;

public sealed record RunStepState(
    string RunId,
    string StepId,
    string Title,
    RunStatus Status,
    string? Output,
    DateTimeOffset UpdatedAtUtc);
