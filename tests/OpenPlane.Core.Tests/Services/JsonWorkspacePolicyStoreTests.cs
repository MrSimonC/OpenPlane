using OpenPlane.Core.Models;
using OpenPlane.Storage.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class JsonWorkspacePolicyStoreTests
{
    [Fact]
    public async Task SaveAndGet_RoundTripPolicy()
    {
        var appName = "OpenPlane.Tests." + Guid.NewGuid().ToString("N");
        var store = new JsonWorkspacePolicyStore(appName);

        var root = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        var policy = new WorkspacePolicy(
            "workspace-1",
            [new PathGrant(root, AllowRead: true, AllowWrite: true, AllowCreate: true)],
            new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "github.com" }));

        await store.SaveAsync(policy, CancellationToken.None);
        var loaded = await store.GetAsync("workspace-1", CancellationToken.None);

        Assert.Single(loaded.PathGrants);
        Assert.Equal(Path.GetFullPath(root), loaded.PathGrants[0].AbsolutePath);
        Assert.Contains("github.com", loaded.NetworkAllowlist.AllowedHosts);
    }
}
