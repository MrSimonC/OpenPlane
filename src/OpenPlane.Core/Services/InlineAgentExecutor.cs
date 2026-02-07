using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class InlineAgentExecutor(
    IFileToolService fileToolService,
    IWorkspacePolicyStore workspacePolicyStore) : IAgentExecutor
{
    public async Task<string> ExecuteStepAsync(PlanStep step, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(step.Details))
        {
            return $"Executed: {step.Title}";
        }

        var details = step.Details.Trim();
        if (!details.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
        {
            return $"Executed: {step.Title}";
        }

        var payload = details["tool:".Length..];
        var separator = payload.IndexOf('|');
        var op = separator >= 0 ? payload[..separator] : payload;
        var argsRaw = separator >= 0 ? payload[(separator + 1)..] : string.Empty;
        var args = argsRaw.Split('|', StringSplitOptions.None);
        var workspaceId = ResolveWorkspaceId();
        var policy = await workspacePolicyStore.GetAsync(workspaceId, cancellationToken);

        switch (op.Trim().ToLowerInvariant())
        {
            case "read":
                EnsureArgumentCount(op, args, 1);
                return await fileToolService.ReadFileAsync(args[0], policy, cancellationToken);
            case "search":
                EnsureArgumentCount(op, args, 2);
                var matches = await fileToolService.SearchFilesAsync(args[0], args[1], policy, cancellationToken);
                return matches.Count == 0 ? "(no matches)" : string.Join(Environment.NewLine, matches);
            case "write":
                EnsureArgumentCount(op, args, 2);
                await fileToolService.WriteFileAsync(args[0], args[1], policy, cancellationToken);
                return $"Wrote file: {Path.GetFullPath(args[0])}";
            case "create-file":
                EnsureArgumentCount(op, args, 2);
                await fileToolService.CreateFileAsync(args[0], args[1], policy, cancellationToken);
                return $"Created file: {Path.GetFullPath(args[0])}";
            case "create-folder":
                EnsureArgumentCount(op, args, 1);
                await fileToolService.CreateFolderAsync(args[0], policy, cancellationToken);
                return $"Created folder: {Path.GetFullPath(args[0])}";
            default:
                return $"Executed: {step.Title}";
        }
    }

    private static void EnsureArgumentCount(string operation, string[] args, int required)
    {
        if (args.Length < required || args.Take(required).Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Invalid `{operation}` tool arguments.");
        }
    }

    private static string ResolveWorkspaceId()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("OPENPLANE_WORKSPACE_ID");
        return string.IsNullOrWhiteSpace(fromEnvironment) ? "default" : fromEnvironment.Trim();
    }
}
