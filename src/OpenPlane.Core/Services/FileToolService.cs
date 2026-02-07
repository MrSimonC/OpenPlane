using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class FileToolService(IAccessPolicyService accessPolicyService) : IFileToolService
{
    public async Task<string> ReadFileAsync(string path, WorkspacePolicy policy, CancellationToken cancellationToken)
    {
        var fullPath = Canonicalize(path);
        EnsureAllowed(accessPolicyService.CanRead(fullPath, policy), $"Read denied by policy for path: {fullPath}");
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    public Task<IReadOnlyList<string>> SearchFilesAsync(string rootPath, string fileNamePattern, WorkspacePolicy policy, CancellationToken cancellationToken)
    {
        var fullRoot = Canonicalize(rootPath);
        EnsureAllowed(accessPolicyService.CanRead(fullRoot, policy), $"Search denied by policy for path: {fullRoot}");

        var pattern = string.IsNullOrWhiteSpace(fileNamePattern) ? "*" : fileNamePattern;
        var files = Directory
            .EnumerateFiles(fullRoot, pattern, SearchOption.AllDirectories)
            .Select(Canonicalize)
            .Where(path => accessPolicyService.CanRead(path, policy))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task WriteFileAsync(string path, string content, WorkspacePolicy policy, CancellationToken cancellationToken)
    {
        var fullPath = Canonicalize(path);
        EnsureAllowed(accessPolicyService.CanWrite(fullPath, policy), $"Write denied by policy for path: {fullPath}");
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    public async Task CreateFileAsync(string path, string content, WorkspacePolicy policy, CancellationToken cancellationToken)
    {
        var fullPath = Canonicalize(path);
        EnsureAllowed(accessPolicyService.CanCreate(fullPath, policy), $"Create denied by policy for path: {fullPath}");

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    public Task CreateFolderAsync(string path, WorkspacePolicy policy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Canonicalize(path);
        EnsureAllowed(accessPolicyService.CanCreate(fullPath, policy), $"Create folder denied by policy for path: {fullPath}");
        Directory.CreateDirectory(fullPath);
        return Task.CompletedTask;
    }

    private static string Canonicalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new PolicyViolationException("Path cannot be empty.");
        }

        return Path.GetFullPath(path);
    }

    private static void EnsureAllowed(bool allowed, string message)
    {
        if (!allowed)
        {
            throw new PolicyViolationException(message);
        }
    }
}
