using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IModelSelectionStore
{
    Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(IReadOnlyDictionary<string, string> selections, CancellationToken cancellationToken);
}

public interface IModelCatalogService
{
    Task<IReadOnlyList<ModelOption>> GetAvailableModelsAsync(CancellationToken cancellationToken);
    Task<ModelSelection> GetModelSelectionAsync(string workspaceId, CancellationToken cancellationToken);
    Task SaveModelSelectionAsync(ModelSelection selection, CancellationToken cancellationToken);
}
