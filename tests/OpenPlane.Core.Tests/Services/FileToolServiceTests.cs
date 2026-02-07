using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class FileToolServiceTests
{
    private readonly FileToolService service = new(new AccessPolicyService());

    [Fact]
    public async Task ReadFileAsync_DeniesOutsideGrant()
    {
        var root = CreateTempDirectory();
        var outsideRoot = CreateTempDirectory();
        var file = Path.Combine(outsideRoot, "secret.txt");
        await File.WriteAllTextAsync(file, "secret");

        var policy = BuildPolicy(root, allowRead: true, allowWrite: true, allowCreate: true);

        var ex = await Assert.ThrowsAsync<PolicyViolationException>(() => service.ReadFileAsync(file, policy, CancellationToken.None));
        Assert.Contains("Read denied", ex.Message);
    }

    [Fact]
    public async Task CreateAndWriteAndRead_RoundTripsInsideGrant()
    {
        var root = CreateTempDirectory();
        var file = Path.Combine(root, "notes", "todo.txt");
        var policy = BuildPolicy(root, allowRead: true, allowWrite: true, allowCreate: true);

        await service.CreateFileAsync(file, "hello", policy, CancellationToken.None);
        await service.WriteFileAsync(file, "updated", policy, CancellationToken.None);
        var content = await service.ReadFileAsync(file, policy, CancellationToken.None);

        Assert.Equal("updated", content);
    }

    [Fact]
    public async Task SearchFilesAsync_ReturnsOnlyGrantedMatches()
    {
        var root = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(root, "a"));
        Directory.CreateDirectory(Path.Combine(root, "b"));
        await File.WriteAllTextAsync(Path.Combine(root, "a", "one.md"), "1");
        await File.WriteAllTextAsync(Path.Combine(root, "b", "two.md"), "2");

        var policy = BuildPolicy(Path.Combine(root, "a"), allowRead: true, allowWrite: false, allowCreate: false);

        var matches = await service.SearchFilesAsync(Path.Combine(root, "a"), "*.md", policy, CancellationToken.None);

        Assert.Single(matches);
        Assert.Contains(Path.Combine(root, "a", "one.md"), matches, StringComparer.OrdinalIgnoreCase);
    }

    private static WorkspacePolicy BuildPolicy(string root, bool allowRead, bool allowWrite, bool allowCreate)
    {
        return new WorkspacePolicy(
            "default",
            [new PathGrant(root, allowRead, allowWrite, allowCreate)],
            new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
