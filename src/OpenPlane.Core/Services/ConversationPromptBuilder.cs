using System.Text;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public static class ConversationPromptBuilder
{
    public static string Build(string currentPrompt, IReadOnlyList<ConversationEntry> historicalEntries)
    {
        if (string.IsNullOrWhiteSpace(currentPrompt))
        {
            return string.Empty;
        }

        if (historicalEntries.Count == 0)
        {
            return currentPrompt.Trim();
        }

        var sb = new StringBuilder();
        sb.AppendLine("Conversation history (oldest to newest):");

        foreach (var entry in historicalEntries
            .OrderBy(x => x.CreatedAtUtc)
            .TakeLast(20))
        {
            var role = string.IsNullOrWhiteSpace(entry.Role) ? "unknown" : entry.Role.Trim().ToLowerInvariant();
            var content = entry.Content ?? string.Empty;
            if (content.Length > 2000)
            {
                content = content[..2000] + " ...[truncated]";
            }

            sb.Append(role);
            sb.Append(": ");
            sb.AppendLine(content);
        }

        sb.AppendLine();
        sb.AppendLine("Respond to the latest user message below using the history above for context:");
        sb.Append(currentPrompt.Trim());
        return sb.ToString();
    }
}
