using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Storage.Services;

namespace OpenPlane.RegressionTests;

public sealed class Gpt5MiniHistoryRegressionTests
{
    [Fact]
    public async Task Gpt5Mini_Run_WritesAssistantHistory_WhenLegacyHistoryFileExists()
    {
        var appName = "OpenPlane.Tests.Regression." + Guid.NewGuid().ToString("N");
        var encryption = new EncryptionService(appName);
        var history = new EncryptedHistoryRepository(encryption, appName);

        await SeedLegacyHistoryArrayAsync(appName, encryption);

        var runner = new SimulatedRunPipeline(history);
        var output = await runner.RunAsync("What is 2 + 2?", "gpt-5-mini", CancellationToken.None);

        Assert.Equal("2 + 2 = 4.", output);

        var entries = await history.GetEntriesAsync("default", CancellationToken.None);
        Assert.True(entries.Count >= 2);
        Assert.Contains(entries, x => x.Role == "assistant" && x.Content == "2 + 2 = 4.");
    }

    private static async Task SeedLegacyHistoryArrayAsync(string appName, EncryptionService encryption)
    {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        Directory.CreateDirectory(basePath);
        var historyPath = Path.Combine(basePath, "history.json");

        var legacyEntries = new[]
        {
            new ConversationEntry("legacy-1", "default", "user", "legacy", DateTimeOffset.UtcNow)
        };

        var plaintext = JsonSerializer.Serialize(legacyEntries);
        var encrypted = await encryption.EncryptAsync(plaintext, CancellationToken.None);
        await File.WriteAllTextAsync(historyPath, encrypted);
    }

    private sealed class SimulatedRunPipeline(IHistoryRepository historyRepository)
    {
        public async Task<string> RunAsync(string prompt, string model, CancellationToken cancellationToken)
        {
            await historyRepository.AddEntryAsync(
                new ConversationEntry(Guid.NewGuid().ToString("N"), "default", "user", prompt, DateTimeOffset.UtcNow),
                cancellationToken);

            var assistant = model switch
            {
                "gpt-5-mini" => "2 + 2 = 4.",
                _ => "unknown model"
            };

            await historyRepository.AddEntryAsync(
                new ConversationEntry(Guid.NewGuid().ToString("N"), "default", "assistant", assistant, DateTimeOffset.UtcNow),
                cancellationToken);

            return assistant;
        }
    }
}
