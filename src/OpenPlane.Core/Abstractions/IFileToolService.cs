using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IFileToolService
{
    Task<string> ReadFileAsync(string path, WorkspacePolicy policy, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> SearchFilesAsync(string rootPath, string fileNamePattern, WorkspacePolicy policy, CancellationToken cancellationToken);
    Task WriteFileAsync(string path, string content, WorkspacePolicy policy, CancellationToken cancellationToken);
    Task CreateFileAsync(string path, string content, WorkspacePolicy policy, CancellationToken cancellationToken);
    Task CreateFolderAsync(string path, WorkspacePolicy policy, CancellationToken cancellationToken);
}
