namespace OpenPlane.Core.Models;

public sealed record PathGrant(string AbsolutePath, bool AllowRead, bool AllowWrite, bool AllowCreate);

public sealed record NetworkAllowlist(IReadOnlySet<string> AllowedHosts);

public sealed record WorkspacePolicy(
    string WorkspaceId,
    IReadOnlyList<PathGrant> PathGrants,
    NetworkAllowlist NetworkAllowlist);
