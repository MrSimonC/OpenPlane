namespace OpenPlane.Core.Abstractions;

public interface IFileAdapterService
{
    Task<string> ReadAsync(string path, CancellationToken cancellationToken);
    Task WriteAsync(string path, string content, CancellationToken cancellationToken);
    bool CanWrite(string path);
    string DescribeCapability(string path);
}
