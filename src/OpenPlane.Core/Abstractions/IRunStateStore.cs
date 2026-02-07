using OpenPlane.Core.Models;

namespace OpenPlane.Core.Abstractions;

public interface IRunStateStore
{
    Task SaveSessionAsync(RunSession session, CancellationToken cancellationToken);
    Task<RunSession?> GetLatestSessionAsync(string workspaceId, CancellationToken cancellationToken);
    Task<RunSession?> GetSessionAsync(string runId, CancellationToken cancellationToken);
    Task<int> RecoverRunningSessionsAsync(CancellationToken cancellationToken);
    Task SaveStepStatesAsync(string runId, IReadOnlyList<RunStepState> steps, CancellationToken cancellationToken);
    Task<IReadOnlyList<RunStepState>> GetStepStatesAsync(string runId, CancellationToken cancellationToken);
}
