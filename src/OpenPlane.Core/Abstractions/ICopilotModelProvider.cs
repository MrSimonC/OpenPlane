namespace OpenPlane.Core.Abstractions;

public interface ICopilotModelProvider
{
    Task<IReadOnlyList<string>> GetModelIdsAsync(CancellationToken cancellationToken);
}
