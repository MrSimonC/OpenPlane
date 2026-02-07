using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Storage.Services;

public sealed class EncryptedHistoryRepository(EncryptionService encryptionService, string appName) : IHistoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int CurrentSchemaVersion = 1;
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

    public async Task ClearEntriesAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var entries = (await ReadAllAsync(cancellationToken))
            .Where(x => !string.Equals(x.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        await WriteAllAsync(entries, cancellationToken);
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

        // Migration path: v0 stored a raw array of ConversationEntry.
        if (TryDeserializeLegacy(content, out var legacy))
        {
            return legacy;
        }

        if (TryDeserializeEnvelope(content, out var envelope))
        {
            return envelope.Entries ?? [];
        }

        return [];
    }

    private static bool TryDeserializeLegacy(string content, out IReadOnlyList<ConversationEntry> entries)
    {
        entries = [];
        try
        {
            var legacy = JsonSerializer.Deserialize<List<ConversationEntry>>(content, JsonOptions);
            if (legacy is null)
            {
                return false;
            }

            entries = legacy;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeserializeEnvelope(string content, out HistoryEnvelope envelope)
    {
        envelope = new HistoryEnvelope(0, []);
        try
        {
            var parsed = JsonSerializer.Deserialize<HistoryEnvelope>(content, JsonOptions);
            if (parsed is null)
            {
                return false;
            }

            envelope = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task WriteAllAsync(IReadOnlyList<ConversationEntry> entries, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(historyPath);
        if (directory is null)
        {
            throw new InvalidOperationException("History path directory is invalid.");
        }

        Directory.CreateDirectory(directory);
        var content = JsonSerializer.Serialize(new HistoryEnvelope(CurrentSchemaVersion, entries.ToArray()), JsonOptions);
        var encrypted = await encryptionService.EncryptAsync(content, cancellationToken);
        await File.WriteAllTextAsync(historyPath, encrypted, cancellationToken);
    }

    private sealed record HistoryEnvelope(int SchemaVersion, IReadOnlyList<ConversationEntry> Entries);
}
