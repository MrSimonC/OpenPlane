using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Storage.Services;

public sealed class JsonExecutionPlanStore(string appName) : IExecutionPlanStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string plansPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "plans.json");

    public async Task SaveLatestAsync(string workspaceId, ExecutionPlan plan, CancellationToken cancellationToken)
    {
        var all = await LoadAllAsync(cancellationToken);
        all[workspaceId] = plan;
        await SaveAllAsync(all, cancellationToken);
    }

    public async Task<ExecutionPlan?> GetLatestAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var all = await LoadAllAsync(cancellationToken);
        return all.TryGetValue(workspaceId, out var plan) ? plan : null;
    }

    private async Task<Dictionary<string, ExecutionPlan>> LoadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(plansPath))
        {
            return new Dictionary<string, ExecutionPlan>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await File.ReadAllTextAsync(plansPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, ExecutionPlan>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, ExecutionPlan>>(json, JsonOptions)
            ?? new Dictionary<string, ExecutionPlan>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveAllAsync(IReadOnlyDictionary<string, ExecutionPlan> plans, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(plansPath)
            ?? throw new InvalidOperationException("Plan store path directory is invalid.");

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(plans, JsonOptions);
        await File.WriteAllTextAsync(plansPath, json, cancellationToken);
    }
}
