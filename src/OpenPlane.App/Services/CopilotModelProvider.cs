using GitHub.Copilot.SDK;
using OpenPlane.Core.Abstractions;

namespace OpenPlane.App.Services;

public sealed class CopilotModelProvider : ICopilotModelProvider
{
    private readonly ICopilotClientOptionsFactory optionsFactory;
    private readonly ICopilotExecutionSettingsStore settingsStore;

    public CopilotModelProvider(ICopilotClientOptionsFactory optionsFactory, ICopilotExecutionSettingsStore settingsStore)
    {
        this.optionsFactory = optionsFactory;
        this.settingsStore = settingsStore;
    }

    public async Task<IReadOnlyList<string>> GetModelIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await settingsStore.LoadAsync(cancellationToken);
            await using var client = new CopilotClient(optionsFactory.Create(settings));

            await client.StartAsync();
            var response = await client.ListModelsAsync();

            var modelIds = response
                .Select(x => x.Id)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (modelIds.Length > 0)
            {
                return modelIds;
            }
        }
        catch
        {
        }

        return ["gpt-5-mini"];
    }
}
