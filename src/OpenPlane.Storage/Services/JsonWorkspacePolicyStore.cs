using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Storage.Services;

public sealed class JsonWorkspacePolicyStore(string appName) : IWorkspacePolicyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "workspace-policies.json");

    public async Task<WorkspacePolicy> GetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var all = await LoadAllAsync(cancellationToken);
        if (all.TryGetValue(workspaceId, out var policy))
        {
            return policy;
        }

        return new WorkspacePolicy(
            workspaceId,
            [],
            new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }

    public async Task SaveAsync(WorkspacePolicy policy, CancellationToken cancellationToken)
    {
        var all = await LoadAllAsync(cancellationToken);
        all[policy.WorkspaceId] = Normalize(policy);
        await SaveAllAsync(all, cancellationToken);
    }

    private async Task<Dictionary<string, WorkspacePolicy>> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath))
        {
            return new Dictionary<string, WorkspacePolicy>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, WorkspacePolicy>(StringComparer.OrdinalIgnoreCase);
        }

        var serialized = JsonSerializer.Deserialize<Dictionary<string, SerializedWorkspacePolicy>>(json, JsonOptions)
            ?? new Dictionary<string, SerializedWorkspacePolicy>(StringComparer.OrdinalIgnoreCase);

        return serialized.ToDictionary(
            pair => pair.Key,
            pair => Deserialize(pair.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveAllAsync(IReadOnlyDictionary<string, WorkspacePolicy> policies, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (directory is null)
        {
            throw new InvalidOperationException("Workspace policy settings path directory is invalid.");
        }

        Directory.CreateDirectory(directory);
        var serialized = policies.ToDictionary(
            pair => pair.Key,
            pair => Serialize(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        var json = JsonSerializer.Serialize(serialized, JsonOptions);
        await File.WriteAllTextAsync(settingsPath, json, cancellationToken);
    }

    private static WorkspacePolicy Normalize(WorkspacePolicy policy)
    {
        var normalizedGrants = policy.PathGrants
            .Select(grant => grant with { AbsolutePath = Path.GetFullPath(grant.AbsolutePath) })
            .DistinctBy(grant => grant.AbsolutePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return policy with
        {
            PathGrants = normalizedGrants,
            NetworkAllowlist = new NetworkAllowlist(new HashSet<string>(policy.NetworkAllowlist.AllowedHosts, StringComparer.OrdinalIgnoreCase))
        };
    }

    private static SerializedWorkspacePolicy Serialize(WorkspacePolicy policy)
    {
        return new SerializedWorkspacePolicy(
            policy.WorkspaceId,
            policy.PathGrants.ToArray(),
            policy.NetworkAllowlist.AllowedHosts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static WorkspacePolicy Deserialize(SerializedWorkspacePolicy policy)
    {
        return Normalize(new WorkspacePolicy(
            policy.WorkspaceId,
            policy.PathGrants,
            new NetworkAllowlist(new HashSet<string>(policy.AllowedHosts, StringComparer.OrdinalIgnoreCase))));
    }

    private sealed record SerializedWorkspacePolicy(
        string WorkspaceId,
        IReadOnlyList<PathGrant> PathGrants,
        IReadOnlyList<string> AllowedHosts);
}
