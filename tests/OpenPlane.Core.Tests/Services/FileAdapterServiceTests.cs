using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class FileAdapterServiceTests
{
    private readonly FileAdapterService service = new();

    [Fact]
    public async Task ReadAsync_Notebook_ExtractsCellText()
    {
        var path = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N") + ".ipynb");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{\"cells\":[{\"cell_type\":\"markdown\",\"source\":[\"hello\\n\"]}]}");

        var output = await service.ReadAsync(path, CancellationToken.None);

        Assert.Contains("cell#1", output);
        Assert.Contains("hello", output);
    }

    [Fact]
    public async Task WriteAsync_Pdf_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N") + ".pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WriteAsync(path, "x", CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_Image_ReturnsExtractOnlyPrefix()
    {
        var path = Path.Combine(Path.GetTempPath(), "openplane-tests", Guid.NewGuid().ToString("N") + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var output = await service.ReadAsync(path, CancellationToken.None);

        Assert.Contains("[extract-only:.png]", output);
        Assert.Contains("size=8 bytes", output);
    }
}
