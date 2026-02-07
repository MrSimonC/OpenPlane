using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface INetworkPolicyService
{
    bool IsAllowedHost(string host, WorkspacePolicy policy);
    IReadOnlySet<string> GetDefaultAllowedHosts();
    WorkspacePolicy WithDefaultAllowlist(string workspaceId, IReadOnlyList<PathGrant> pathGrants);
}
