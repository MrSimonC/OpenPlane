namespace OpenPlane.Core.Models;

public sealed record ConversationEntry(
    string Id,
    string WorkspaceId,
    string Role,
    string Content,
    DateTimeOffset CreatedAtUtc);
