namespace OpenPlane.Core.Models;

public sealed record ConnectorDefinition(
    string Name,
    string Command,
    IReadOnlyDictionary<string, string> EnvironmentVariables,
    IReadOnlyList<string> AllowedScopes);

public sealed record ConnectorStatus(string Name, bool Connected, string? LastError);
