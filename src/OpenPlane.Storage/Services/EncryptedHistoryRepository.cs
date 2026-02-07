using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Storage.Services;

public sealed class EncryptedHistoryRepository(EncryptionService encryptionService, string appName) : IHistoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string historyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "history.json");

    public async Task AddEntryAsync(ConversationEntry entry, CancellationToken cancellationToken)
    {
        var entries = (await ReadAllAsync(cancellationToken)).ToList();
        entries.Add(entry);
        await WriteAllAsync(entries, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationEntry>> GetEntriesAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var entries = await ReadAllAsync(cancellationToken);
        return entries.Where(x => string.Equals(x.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private async Task<IReadOnlyList<ConversationEntry>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(historyPath))
        {
            return [];
        }

        var encrypted = await File.ReadAllTextAsync(historyPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            return [];
        }

        var content = await encryptionService.DecryptAsync(encrypted, cancellationToken);
        return JsonSerializer.Deserialize<List<ConversationEntry>>(content, JsonOptions) ?? [];
    }

    private async Task WriteAllAsync(IReadOnlyList<ConversationEntry> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(historyPath);
        if (directory is null)
        {
            throw new InvalidOperationException("History path directory is invalid.");
        }

        Directory.CreateDirectory(directory);
        var content = JsonSerializer.Serialize(entries, JsonOptions);
        var encrypted = await encryptionService.EncryptAsync(content, cancellationToken);
        await File.WriteAllTextAsync(historyPath, encrypted, cancellationToken);
    }
}
