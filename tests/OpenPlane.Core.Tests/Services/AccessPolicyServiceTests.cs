using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class AccessPolicyServiceTests
{
    private readonly AccessPolicyService service = new();

    [Fact]
    public void CanRead_ReturnsTrueInsideGrant()
    {
        var root = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "a", "file.txt");
        var policy = new WorkspacePolicy(
            "default",
            [new PathGrant(Path.Combine(root, "a"), AllowRead: true, AllowWrite: false, AllowCreate: false)],
            new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        var allowed = service.CanRead(filePath, policy);

        Assert.True(allowed);
    }

    [Fact]
    public void CanRead_ReturnsFalseOutsideGrant()
    {
        var root = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(root, "not-allowed", "file.txt");
        var policy = new WorkspacePolicy(
            "default",
            [new PathGrant(Path.Combine(root, "allowed"), AllowRead: true, AllowWrite: false, AllowCreate: false)],
            new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        var allowed = service.CanRead(filePath, policy);

        Assert.False(allowed);
    }
}
