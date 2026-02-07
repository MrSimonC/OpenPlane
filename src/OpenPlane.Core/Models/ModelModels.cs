namespace OpenPlane.Core.Models;

public sealed record ModelOption(string Id, string DisplayName, bool IsDefault, IReadOnlySet<string> Capabilities);

public sealed record ModelSelection(string WorkspaceId, string ModelId);
