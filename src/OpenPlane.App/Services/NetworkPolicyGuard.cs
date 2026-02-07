using OpenPlane.Core.Abstractions;

namespace OpenPlane.App.Services;

public interface INetworkPolicyGuard
{
    Task EnsureDefaultCopilotHostsAllowedAsync(CancellationToken cancellationToken);
    Task EnsureUrlAllowedAsync(string url, CancellationToken cancellationToken);
}

public sealed class NetworkPolicyGuard(
    IWorkspaceSettingsStore workspaceSettingsStore,
    IWorkspacePolicyStore workspacePolicyStore,
    INetworkPolicyService networkPolicyService) : INetworkPolicyGuard
{
    public async Task EnsureDefaultCopilotHostsAllowedAsync(CancellationToken cancellationToken)
    {
        var policy = await GetActiveWorkspacePolicyAsync(cancellationToken);
        foreach (var host in networkPolicyService.GetDefaultAllowedHosts())
        {
            if (!networkPolicyService.IsAllowedHost(host, policy))
            {
                throw new InvalidOperationException($"[NetworkDenied] Host not allowlisted: {host}");
            }
        }
    }

    public async Task EnsureUrlAllowedAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("[NetworkDenied] URL is invalid.");
        }

        var policy = await GetActiveWorkspacePolicyAsync(cancellationToken);
        if (!networkPolicyService.IsAllowedHost(uri.Host, policy))
        {
            throw new InvalidOperationException($"[NetworkDenied] Host not allowlisted: {uri.Host}");
        }
    }

    private async Task<OpenPlane.Core.Models.WorkspacePolicy> GetActiveWorkspacePolicyAsync(CancellationToken cancellationToken)
    {
        var workspaceSettings = await workspaceSettingsStore.LoadAsync(cancellationToken);
        var workspaceId = workspaceSettings.SelectedWorkspaceId;
        return await workspacePolicyStore.GetAsync(workspaceId, cancellationToken);
    }
}
