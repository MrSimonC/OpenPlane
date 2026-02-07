using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class NetworkPolicyService : INetworkPolicyService
{
    private static readonly IReadOnlySet<string> DefaultHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "api.github.com",
        "copilot-proxy.githubusercontent.com",
        "models.inference.ai.azure.com"
    };

    public bool IsAllowedHost(string host, WorkspacePolicy policy)
    {
        return policy.NetworkAllowlist.AllowedHosts.Contains(host);
    }

    public IReadOnlySet<string> GetDefaultAllowedHosts()
    {
        return DefaultHosts;
    }

    public WorkspacePolicy WithDefaultAllowlist(string workspaceId, IReadOnlyList<PathGrant> pathGrants)
    {
        return new WorkspacePolicy(workspaceId, pathGrants, new NetworkAllowlist(DefaultHosts));
    }
}
