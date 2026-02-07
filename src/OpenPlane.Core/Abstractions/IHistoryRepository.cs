using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IHistoryRepository
{
    Task AddEntryAsync(ConversationEntry entry, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConversationEntry>> GetEntriesAsync(string workspaceId, CancellationToken cancellationToken);
}
