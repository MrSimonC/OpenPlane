using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class AccessPolicyService : IAccessPolicyService
{
    public bool CanRead(string path, WorkspacePolicy policy)
    {
        return IsAllowed(path, policy.PathGrants, grant => grant.AllowRead);
    }

    public bool CanWrite(string path, WorkspacePolicy policy)
    {
        return IsAllowed(path, policy.PathGrants, grant => grant.AllowWrite);
    }

    public bool CanCreate(string path, WorkspacePolicy policy)
    {
        return IsAllowed(path, policy.PathGrants, grant => grant.AllowCreate);
    }

    private static bool IsAllowed(string path, IReadOnlyList<PathGrant> grants, Func<PathGrant, bool> rule)
    {
        var candidate = NormalizePath(path);

        foreach (var grant in grants)
        {
            if (!rule(grant))
            {
                continue;
            }

            var scope = NormalizePath(grant.AbsolutePath);
            if (IsWithinScope(candidate, scope))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static bool IsWithinScope(string candidate, string scope)
    {
        if (string.Equals(candidate, scope, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var withSeparator = scope + Path.DirectorySeparatorChar;
        return candidate.StartsWith(withSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
