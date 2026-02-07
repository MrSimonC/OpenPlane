using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class ModelCatalogServiceTests
{
    [Fact]
    public async Task GetAvailableModelsAsync_EnsuresDefaultModelExists()
    {
        var provider = new FakeProvider(["gpt-5"]);
        var store = new InMemoryModelSelectionStore();
        var service = new ModelCatalogService(provider, store);

        var models = await service.GetAvailableModelsAsync(CancellationToken.None);

        Assert.Contains(models, x => x.Id == "gpt-5-mini" && x.IsDefault);
    }

    [Fact]
    public async Task SaveAndLoadSelection_PersistsPerWorkspace()
    {
        var provider = new FakeProvider(["gpt-5-mini"]);
        var store = new InMemoryModelSelectionStore();
        var service = new ModelCatalogService(provider, store);

        await service.SaveModelSelectionAsync(new ModelSelection("workspace-1", "gpt-5-mini"), CancellationToken.None);
        var loaded = await service.GetModelSelectionAsync("workspace-1", CancellationToken.None);

        Assert.Equal("gpt-5-mini", loaded.ModelId);
    }

    private sealed class FakeProvider(IReadOnlyList<string> modelIds) : ICopilotModelProvider
    {
        public Task<IReadOnlyList<string>> GetModelIdsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(modelIds);
        }
    }

    private sealed class InMemoryModelSelectionStore : IModelSelectionStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));
        }

        public Task SaveAsync(IReadOnlyDictionary<string, string> selections, CancellationToken cancellationToken)
        {
            values.Clear();
            foreach (var pair in selections)
            {
                values[pair.Key] = pair.Value;
            }

            return Task.CompletedTask;
        }
    }
}
