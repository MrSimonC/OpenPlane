using System.Text.RegularExpressions;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class PromptAttachmentResolver(IAccessPolicyService accessPolicyService) : IPromptAttachmentResolver
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"
    };

    private static readonly Regex FileTokenRegex = new(@"[\w\-/\\\.]+\.[a-zA-Z0-9]{2,8}", RegexOptions.Compiled);

    public Task<IReadOnlyList<string>> ResolveAsync(string prompt, WorkspacePolicy policy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(prompt) || policy.PathGrants.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var promptLower = prompt.Trim().ToLowerInvariant();
        var wantsImages = ContainsAny(promptLower, "image", "images", "photo", "screenshot", "picture");
        var wantsFiles = wantsImages || ContainsAny(promptLower, "file", "files", "document", "documents", "analyze", "summarize", "read");
        if (!wantsFiles)
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var selected = new List<string>();

        foreach (Match match in FileTokenRegex.Matches(prompt))
        {
            var token = match.Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            AddMatchesForToken(token, policy, selected, cancellationToken);
        }

        if (selected.Count == 0)
        {
            AddRecentMatches(policy, wantsImages, selected, cancellationToken);
        }

        var distinct = selected
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(distinct);
    }

    private void AddMatchesForToken(string token, WorkspacePolicy policy, List<string> selected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var maybePath = token.Replace("\\ ", " ");
        if (Path.IsPathRooted(maybePath) && File.Exists(maybePath))
        {
            var full = Path.GetFullPath(maybePath);
            if (accessPolicyService.CanRead(full, policy))
            {
                selected.Add(full);
            }
            return;
        }

        var fileName = Path.GetFileName(maybePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        foreach (var grant in policy.PathGrants.Where(x => x.AllowRead))
        {
            if (string.IsNullOrWhiteSpace(grant.AbsolutePath))
            {
                continue;
            }

            var root = Path.GetFullPath(grant.AbsolutePath);
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (var path in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).Take(20))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var full = Path.GetFullPath(path);
                    if (accessPolicyService.CanRead(full, policy))
                    {
                        selected.Add(full);
                    }
                }
            }
            catch
            {
            }
        }
    }

    private void AddRecentMatches(WorkspacePolicy policy, bool imagesOnly, List<string> selected, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = new List<string>();
        foreach (var grant in policy.PathGrants.Where(x => x.AllowRead))
        {
            if (string.IsNullOrWhiteSpace(grant.AbsolutePath))
            {
                continue;
            }

            var root = Path.GetFullPath(grant.AbsolutePath);
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Take(2000))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var full = Path.GetFullPath(path);
                    if (!accessPolicyService.CanRead(full, policy))
                    {
                        continue;
                    }

                    var extension = Path.GetExtension(full);
                    if (imagesOnly && !ImageExtensions.Contains(extension))
                    {
                        continue;
                    }

                    candidates.Add(full);
                }
            }
            catch
            {
            }
        }

        var ordered = candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path =>
            {
                try
                {
                    return File.GetLastWriteTimeUtc(path);
                }
                catch
                {
                    return DateTime.MinValue;
                }
            })
            .Take(6);

        selected.AddRange(ordered);
    }

    private static bool ContainsAny(string input, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (input.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
