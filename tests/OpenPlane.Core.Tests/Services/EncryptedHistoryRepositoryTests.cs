using System.Text.Json;
using OpenPlane.Core.Models;
using OpenPlane.Storage.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class EncryptedHistoryRepositoryTests
{
    [Fact]
    public async Task AddEntryAsync_ReadsLegacyArrayAndWritesEnvelopeWithoutThrowing()
    {
        var appName = "OpenPlane.Tests.History." + Guid.NewGuid().ToString("N");
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        Directory.CreateDirectory(basePath);

        var keyPath = Path.Combine(basePath, "history.key");
        var historyPath = Path.Combine(basePath, "history.json");

        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await File.WriteAllTextAsync(keyPath, key);

        var legacyEntries = new List<ConversationEntry>
        {
            new("1", "default", "user", "hello", DateTimeOffset.UtcNow)
        };

        var encryption = new EncryptionService(appName);
        var encryptedLegacy = await encryption.EncryptAsync(JsonSerializer.Serialize(legacyEntries), CancellationToken.None);
        await File.WriteAllTextAsync(historyPath, encryptedLegacy);

        var repository = new EncryptedHistoryRepository(encryption, appName);
        await repository.AddEntryAsync(new ConversationEntry("2", "default", "assistant", "world", DateTimeOffset.UtcNow), CancellationToken.None);

        var loaded = await repository.GetEntriesAsync("default", CancellationToken.None);
        Assert.True(loaded.Count >= 2);
    }

    [Fact]
    public async Task ClearEntriesAsync_RemovesOnlyRequestedWorkspace()
    {
        var appName = "OpenPlane.Tests.History." + Guid.NewGuid().ToString("N");
        var encryption = new EncryptionService(appName);
        var repository = new EncryptedHistoryRepository(encryption, appName);

        await repository.AddEntryAsync(new ConversationEntry("1", "workspace-a", "user", "a", DateTimeOffset.UtcNow), CancellationToken.None);
        await repository.AddEntryAsync(new ConversationEntry("2", "workspace-b", "user", "b", DateTimeOffset.UtcNow), CancellationToken.None);

        await repository.ClearEntriesAsync("workspace-a", CancellationToken.None);

        var a = await repository.GetEntriesAsync("workspace-a", CancellationToken.None);
        var b = await repository.GetEntriesAsync("workspace-b", CancellationToken.None);
        Assert.Empty(a);
        Assert.Single(b);
    }
}
