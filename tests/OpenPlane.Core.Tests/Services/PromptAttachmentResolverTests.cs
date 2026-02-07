using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class PromptAttachmentResolverTests
{
    private readonly PromptAttachmentResolver resolver = new(new AccessPolicyService());

    [Fact]
    public async Task ResolveAsync_ImagePrompt_ReturnsGrantedImage()
    {
        var root = CreateTempDirectory();
        var imagePath = Path.Combine(root, "photo.png");
        await File.WriteAllBytesAsync(imagePath, [0x89, 0x50, 0x4E, 0x47]);

        var policy = BuildPolicy(root);
        var result = await resolver.ResolveAsync("List images in the workspace", policy, CancellationToken.None);

        Assert.Contains(Path.GetFullPath(imagePath), result, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_ExplicitFileName_RespectsGrantScope()
    {
        var grantedRoot = CreateTempDirectory();
        var outsideRoot = CreateTempDirectory();
        var grantedPath = Path.Combine(grantedRoot, "report.txt");
        var outsidePath = Path.Combine(outsideRoot, "report.txt");
        await File.WriteAllTextAsync(grantedPath, "inside");
        await File.WriteAllTextAsync(outsidePath, "outside");

        var policy = BuildPolicy(grantedRoot);
        var result = await resolver.ResolveAsync("Please analyze report.txt", policy, CancellationToken.None);

        Assert.Contains(Path.GetFullPath(grantedPath), result, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetFullPath(outsidePath), result, StringComparer.OrdinalIgnoreCase);
    }

    private static WorkspacePolicy BuildPolicy(string root)
    {
        return new WorkspacePolicy(
            "default",
            [new PathGrant(root, AllowRead: true, AllowWrite: true, AllowCreate: true)],
            new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
