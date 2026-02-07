using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.Core.Tests.Services;

public sealed class InlineAgentExecutorTests
{
    [Fact]
    public async Task ExecuteStepAsync_UsesWorkspaceIdFromEnvironmentForPolicyLookup()
    {
        const string workspaceId = "workspace-2";
        var previous = Environment.GetEnvironmentVariable("OPENPLANE_WORKSPACE_ID");
        Environment.SetEnvironmentVariable("OPENPLANE_WORKSPACE_ID", workspaceId);

        try
        {
            var fileTool = new CapturingFileToolService();
            var policyStore = new CapturingWorkspacePolicyStore();
            var executor = new InlineAgentExecutor(fileTool, policyStore);
            var step = new PlanStep(Guid.NewGuid().ToString("N"), "List files", "tool:search|/tmp|*", SignificantAction: false);

            await executor.ExecuteStepAsync(step, CancellationToken.None);

            Assert.Equal(workspaceId, policyStore.LastWorkspaceIdRequested);
            Assert.NotNull(fileTool.LastPolicySeen);
            Assert.Equal(workspaceId, fileTool.LastPolicySeen!.WorkspaceId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENPLANE_WORKSPACE_ID", previous);
        }
    }

    private sealed class CapturingWorkspacePolicyStore : IWorkspacePolicyStore
    {
        public string? LastWorkspaceIdRequested { get; private set; }

        public Task<WorkspacePolicy> GetAsync(string workspaceId, CancellationToken cancellationToken)
        {
            LastWorkspaceIdRequested = workspaceId;
            var policy = new WorkspacePolicy(
                workspaceId,
                [new PathGrant("/tmp", AllowRead: true, AllowWrite: true, AllowCreate: true)],
                new NetworkAllowlist(new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
            return Task.FromResult(policy);
        }

        public Task SaveAsync(WorkspacePolicy policy, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingFileToolService : IFileToolService
    {
        public WorkspacePolicy? LastPolicySeen { get; private set; }

        public Task<string> ReadFileAsync(string path, WorkspacePolicy policy, CancellationToken cancellationToken)
        {
            LastPolicySeen = policy;
            return Task.FromResult("content");
        }

        public Task<IReadOnlyList<string>> SearchFilesAsync(string rootPath, string fileNamePattern, WorkspacePolicy policy, CancellationToken cancellationToken)
        {
            LastPolicySeen = policy;
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        public Task WriteFileAsync(string path, string content, WorkspacePolicy policy, CancellationToken cancellationToken)
        {
            LastPolicySeen = policy;
            return Task.CompletedTask;
        }

        public Task CreateFileAsync(string path, string content, WorkspacePolicy policy, CancellationToken cancellationToken)
        {
            LastPolicySeen = policy;
            return Task.CompletedTask;
        }

        public Task CreateFolderAsync(string path, WorkspacePolicy policy, CancellationToken cancellationToken)
        {
            LastPolicySeen = policy;
            return Task.CompletedTask;
        }
    }
}
