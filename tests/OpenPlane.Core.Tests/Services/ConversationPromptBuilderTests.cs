using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class ConversationPromptBuilderTests
{
    [Fact]
    public void Build_NoHistory_ReturnsPrompt()
    {
        var prompt = "What is next?";
        var output = ConversationPromptBuilder.Build(prompt, []);
        Assert.Equal(prompt, output);
    }

    [Fact]
    public void Build_WithHistory_EmbedsRecentTurnsAndLatestPrompt()
    {
        var entries = new List<ConversationEntry>
        {
            new("1", "default", "user", "first question", DateTimeOffset.UtcNow.AddMinutes(-2)),
            new("2", "default", "assistant", "first answer", DateTimeOffset.UtcNow.AddMinutes(-1))
        };

        var output = ConversationPromptBuilder.Build("new question", entries);

        Assert.Contains("Conversation history", output);
        Assert.Contains("user: first question", output);
        Assert.Contains("assistant: first answer", output);
        Assert.Contains("new question", output);
    }
}
