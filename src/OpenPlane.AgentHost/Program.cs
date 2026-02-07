using System.Text.Json;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;
using OpenPlane.Connectors.Mcp.Services;
using OpenPlane.Storage.Services;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var connectorRegistry = new JsonConnectorRegistry("OpenPlane");
await using var connectorBroker = new ProcessMcpConnectorBroker();
var workspacePoliciesPath = Environment.GetEnvironmentVariable("OPENPLANE_WORKSPACE_POLICIES_PATH");
var executor = new InlineAgentExecutor(
    new FileToolService(new AccessPolicyService()),
    new JsonWorkspacePolicyStore("OpenPlane", workspacePoliciesPath));

if (args.Length > 0)
{
    await RunSingleStepAsync(executor, args, CancellationToken.None);
    return;
}

await RunStdioServerAsync(executor, connectorRegistry, connectorBroker, jsonOptions, CancellationToken.None);

static async Task RunStdioServerAsync(
    IAgentExecutor executor,
    IConnectorRegistry connectorRegistry,
    IMcpConnectorBroker connectorBroker,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var line = await Console.In.ReadLineAsync();
        if (line is null)
        {
            break;
        }

        WorkerResponse response;
        try
        {
            var request = JsonSerializer.Deserialize<WorkerRequest>(line, jsonOptions);
            if (request is null)
            {
                response = new WorkerResponse(
                    Guid.NewGuid().ToString("N"),
                    false,
                    null,
                    "invalid_request",
                    "Request payload is invalid.",
                    DateTimeOffset.UtcNow);
            }
            else
            {
                response = await HandleRequestAsync(executor, connectorRegistry, connectorBroker, request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            response = new WorkerResponse(
                Guid.NewGuid().ToString("N"),
                false,
                null,
                "unhandled_error",
                ex.Message,
                DateTimeOffset.UtcNow);
        }

        var payload = JsonSerializer.Serialize(response, jsonOptions);
        await Console.Out.WriteLineAsync(payload);
        await Console.Out.FlushAsync();
    }
}

static async Task<WorkerResponse> HandleRequestAsync(
    IAgentExecutor executor,
    IConnectorRegistry connectorRegistry,
    IMcpConnectorBroker connectorBroker,
    WorkerRequest request,
    CancellationToken cancellationToken)
{
    var workspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? "default" : request.WorkspaceId.Trim();
    Environment.SetEnvironmentVariable("OPENPLANE_WORKSPACE_ID", workspaceId);

    if (request.Type == WorkerRequestType.Ping)
    {
        return new WorkerResponse(request.RequestId, true, "pong", null, null, DateTimeOffset.UtcNow);
    }

    if (request.Type != WorkerRequestType.ExecuteStep || request.Step is null)
    {
        return new WorkerResponse(request.RequestId, false, null, "invalid_request", "ExecuteStep request requires a step payload.", DateTimeOffset.UtcNow);
    }

    try
    {
        var connectorResult = await TryHandleConnectorStepAsync(connectorRegistry, connectorBroker, request.Step, cancellationToken);
        if (connectorResult.Handled)
        {
            return new WorkerResponse(request.RequestId, connectorResult.Success, connectorResult.Output, connectorResult.ErrorCode, connectorResult.ErrorMessage, DateTimeOffset.UtcNow);
        }

        var output = await executor.ExecuteStepAsync(request.Step, cancellationToken);
        return new WorkerResponse(request.RequestId, true, output, null, null, DateTimeOffset.UtcNow);
    }
    catch (PolicyViolationException ex)
    {
        return new WorkerResponse(request.RequestId, false, null, "policy_violation", ex.Message, DateTimeOffset.UtcNow);
    }
    catch (Exception ex)
    {
        return new WorkerResponse(request.RequestId, false, null, "execution_error", ex.Message, DateTimeOffset.UtcNow);
    }
}

static async Task<(bool Handled, bool Success, string? Output, string? ErrorCode, string? ErrorMessage)> TryHandleConnectorStepAsync(
    IConnectorRegistry connectorRegistry,
    IMcpConnectorBroker connectorBroker,
    PlanStep step,
    CancellationToken cancellationToken)
{
    var details = step.Details?.Trim() ?? string.Empty;
    if (!details.StartsWith("tool:mcp:", StringComparison.OrdinalIgnoreCase))
    {
        return (false, false, null, null, null);
    }

    var payload = details["tool:mcp:".Length..];
    var parts = payload.Split('|', StringSplitOptions.None);
    var op = parts.FirstOrDefault()?.Trim().ToLowerInvariant();

    switch (op)
    {
        case "list":
            {
                var connectors = await connectorRegistry.GetAllAsync(cancellationToken);
                var statuses = await connectorBroker.GetStatusesAsync(cancellationToken);
                var statusMap = statuses.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
                var output = string.Join(
                    Environment.NewLine,
                    connectors.Select(c =>
                    {
                        statusMap.TryGetValue(c.Name, out var status);
                        var state = status?.Connected == true ? "connected" : "disconnected";
                        return $"{c.Name} [{state}] => {c.Command}";
                    }));
                return (true, true, string.IsNullOrWhiteSpace(output) ? "(no connectors)" : output, null, null);
            }

        case "connect":
            {
                var name = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                var all = await connectorRegistry.GetAllAsync(cancellationToken);
                var definition = all.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (definition is null)
                {
                    return (true, false, null, "connector_not_found", $"Connector not found: {name}");
                }

                var status = await connectorBroker.ConnectAsync(definition, cancellationToken);
                return status.Connected
                    ? (true, true, $"Connector connected: {status.Name}", null, null)
                    : (true, false, null, "connector_connect_failed", status.LastError ?? "Connector failed to connect.");
            }

        case "disconnect":
            {
                var name = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                await connectorBroker.DisconnectAsync(name, cancellationToken);
                return (true, true, $"Connector disconnected: {name}", null, null);
            }

        default:
            return (true, false, null, "invalid_mcp_tool", "Unsupported MCP tool command. Use tool:mcp:list|connect|disconnect.");
    }
}

static async Task RunSingleStepAsync(IAgentExecutor executor, string[] args, CancellationToken cancellationToken)
{
    var title = args.Length > 0 ? args[0] : "No step title provided";
    var details = args.Length > 1 ? args[1] : "No details provided";
    var step = new PlanStep(Guid.NewGuid().ToString("N"), title, details, SignificantAction: true);

    var output = await executor.ExecuteStepAsync(step, cancellationToken);
    Console.WriteLine(output);
}
