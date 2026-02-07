using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Storage.Services;

public sealed class JsonRunStateStore(string appName) : IRunStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string sessionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "run-sessions.json");
    private readonly string stepsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName, "run-steps.json");

    public async Task SaveSessionAsync(RunSession session, CancellationToken cancellationToken)
    {
        var all = await LoadSessionsAsync(cancellationToken);
        all[session.RunId] = session;
        await SaveSessionsAsync(all, cancellationToken);
    }

    public async Task<RunSession?> GetLatestSessionAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var all = await LoadSessionsAsync(cancellationToken);
        return all.Values
            .Where(x => string.Equals(x.WorkspaceId, workspaceId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();
    }

    public async Task<RunSession?> GetSessionAsync(string runId, CancellationToken cancellationToken)
    {
        var all = await LoadSessionsAsync(cancellationToken);
        return all.TryGetValue(runId, out var session) ? session : null;
    }

    public async Task SaveStepStatesAsync(string runId, IReadOnlyList<RunStepState> steps, CancellationToken cancellationToken)
    {
        var all = await LoadStepStatesAsync(cancellationToken);
        all[runId] = steps.ToArray();
        await SaveStepStatesAsync(all, cancellationToken);
    }

    public async Task<IReadOnlyList<RunStepState>> GetStepStatesAsync(string runId, CancellationToken cancellationToken)
    {
        var all = await LoadStepStatesAsync(cancellationToken);
        return all.TryGetValue(runId, out var steps) ? steps : [];
    }

    private async Task<Dictionary<string, RunSession>> LoadSessionsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(sessionsPath))
        {
            return new Dictionary<string, RunSession>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await File.ReadAllTextAsync(sessionsPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, RunSession>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, RunSession>>(json, JsonOptions)
            ?? new Dictionary<string, RunSession>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveSessionsAsync(IReadOnlyDictionary<string, RunSession> sessions, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(sessionsPath)
            ?? throw new InvalidOperationException("Run sessions path directory is invalid.");

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(sessions, JsonOptions);
        await File.WriteAllTextAsync(sessionsPath, json, cancellationToken);
    }

    private async Task<Dictionary<string, RunStepState[]>> LoadStepStatesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(stepsPath))
        {
            return new Dictionary<string, RunStepState[]>(StringComparer.OrdinalIgnoreCase);
        }

        var json = await File.ReadAllTextAsync(stepsPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, RunStepState[]>(StringComparer.OrdinalIgnoreCase);
        }

        return JsonSerializer.Deserialize<Dictionary<string, RunStepState[]>>(json, JsonOptions)
            ?? new Dictionary<string, RunStepState[]>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SaveStepStatesAsync(IReadOnlyDictionary<string, RunStepState[]> steps, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(stepsPath)
            ?? throw new InvalidOperationException("Run steps path directory is invalid.");

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(steps, JsonOptions);
        await File.WriteAllTextAsync(stepsPath, json, cancellationToken);
    }
}
