using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IPromptAttachmentResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(string prompt, WorkspacePolicy policy, CancellationToken cancellationToken);
}
