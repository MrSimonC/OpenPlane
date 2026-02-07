using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Core.Services;

public sealed class ModelCatalogService(
    ICopilotModelProvider modelProvider,
    IModelSelectionStore modelSelectionStore) : IModelCatalogService
{
    private const string DefaultModelId = "gpt-5-mini";

    public async Task<IReadOnlyList<ModelOption>> GetAvailableModelsAsync(CancellationToken cancellationToken)
    {
        var ids = await modelProvider.GetModelIdsAsync(cancellationToken);
        if (!ids.Contains(DefaultModelId, StringComparer.OrdinalIgnoreCase))
        {
            ids = ids.Append(DefaultModelId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        return ids
            .Select(id => new ModelOption(id, id, string.Equals(id, DefaultModelId, StringComparison.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "chat", "tool_use" }))
            .OrderByDescending(option => option.IsDefault)
            .ThenBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<ModelSelection> GetModelSelectionAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var saved = await modelSelectionStore.LoadAsync(cancellationToken);
        if (saved.TryGetValue(workspaceId, out var modelId))
        {
            return new ModelSelection(workspaceId, modelId);
        }

        return new ModelSelection(workspaceId, DefaultModelId);
    }

    public async Task SaveModelSelectionAsync(ModelSelection selection, CancellationToken cancellationToken)
    {
        var saved = new Dictionary<string, string>(await modelSelectionStore.LoadAsync(cancellationToken), StringComparer.OrdinalIgnoreCase)
        {
            [selection.WorkspaceId] = selection.ModelId
        };

        await modelSelectionStore.SaveAsync(saved, cancellationToken);
    }
}
