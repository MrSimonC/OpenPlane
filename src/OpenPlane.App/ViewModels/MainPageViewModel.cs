using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.ApplicationModel;
using OpenPlane.App.Services;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

namespace OpenPlane.App.ViewModels;

public sealed class MainPageViewModel(
    IModelCatalogService modelCatalogService,
    IHistoryRepository historyRepository,
    IWorkspacePolicyStore workspacePolicyStore,
    INetworkPolicyService networkPolicyService,
    IConnectorRegistry connectorRegistry,
    IMcpConnectorBroker connectorBroker,
    IAgentExecutor agentExecutor,
    INetworkPolicyGuard networkPolicyGuard,
    ILocalLogService localLogService,
    ICopilotAuthService authService,
    ICopilotExecutionService executionService,
    IPromptAttachmentResolver promptAttachmentResolver,
    IPlanExecutionService planExecutionService,
    IWorkspaceSettingsStore workspaceSettingsStore,
    ICopilotHealthService healthService) : INotifyPropertyChanged
{
    private const string DefaultWorkspaceId = "default";
    private string prompt = "What is 2 + 2?";
    private string selectedModel = "gpt-5-mini";
    private bool isAuthenticated;
    private string authStatus = "Checking Copilot login status...";
    private bool isBusy;
    private string effectiveConnection = string.Empty;
    private string cliVersion = "n/a";
    private string lastStartupError = "None";
    private string modelProbeStatus = "Unknown";
    private string deviceCode = string.Empty;
    private string verificationUrl = string.Empty;
    private string manualCommandGuidance = string.Empty;
    private string newGrantPath = string.Empty;
    private string newAllowedHost = string.Empty;
    private string selectedWorkspaceId = DefaultWorkspaceId;
    private string newWorkspaceId = string.Empty;
    private string currentPlanSummary = "No plan created.";
    private bool isCurrentPlanApproved;
    private string latestRunStatus = "No run session.";
    private string newConnectorName = string.Empty;
    private string newConnectorCommand = string.Empty;
    private bool canStop;
    private CancellationTokenSource? activeOperationCts;
    private string? activeOperationName;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Models { get; } = [];

    public ObservableCollection<string> Timeline { get; } = [];
    public ObservableCollection<string> GrantedFolders { get; } = [];
    public ObservableCollection<string> AllowedHosts { get; } = [];
    public ObservableCollection<string> Workspaces { get; } = [];
    public ObservableCollection<ConnectorStatusViewItem> ConnectorStatuses { get; } = [];

    public string Prompt
    {
        get => prompt;
        set
        {
            if (SetProperty(ref prompt, value))
            {
                OnPropertyChanged(nameof(CanRun));
            }
        }
    }

    public string SelectedModel
    {
        get => selectedModel;
        set => SetProperty(ref selectedModel, value);
    }

    public string NewGrantPath
    {
        get => newGrantPath;
        set => SetProperty(ref newGrantPath, value);
    }

    public string SelectedWorkspaceId
    {
        get => selectedWorkspaceId;
        private set => SetProperty(ref selectedWorkspaceId, value);
    }

    public string NewWorkspaceId
    {
        get => newWorkspaceId;
        set => SetProperty(ref newWorkspaceId, value);
    }

    public string NewAllowedHost
    {
        get => newAllowedHost;
        set => SetProperty(ref newAllowedHost, value);
    }

    public string NewConnectorName
    {
        get => newConnectorName;
        set => SetProperty(ref newConnectorName, value);
    }

    public string NewConnectorCommand
    {
        get => newConnectorCommand;
        set => SetProperty(ref newConnectorCommand, value);
    }

    public string CurrentPlanSummary
    {
        get => currentPlanSummary;
        private set => SetProperty(ref currentPlanSummary, value);
    }

    public bool IsCurrentPlanApproved
    {
        get => isCurrentPlanApproved;
        private set
        {
            if (SetProperty(ref isCurrentPlanApproved, value))
            {
                OnPropertyChanged(nameof(CanApprovePlan));
                OnPropertyChanged(nameof(CanExecutePlan));
            }
        }
    }

    public string LatestRunStatus
    {
        get => latestRunStatus;
        private set => SetProperty(ref latestRunStatus, value);
    }

    public bool IsAuthenticated
    {
        get => isAuthenticated;
        private set
        {
            if (SetProperty(ref isAuthenticated, value))
            {
                OnPropertyChanged(nameof(CanRun));
            }
        }
    }

    public string AuthStatus
    {
        get => authStatus;
        private set => SetProperty(ref authStatus, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanRun));
            }
        }
    }

    public string EffectiveConnection
    {
        get => effectiveConnection;
        private set => SetProperty(ref effectiveConnection, value);
    }

    public string CliVersion
    {
        get => cliVersion;
        private set => SetProperty(ref cliVersion, value);
    }

    public string LastStartupError
    {
        get => lastStartupError;
        private set => SetProperty(ref lastStartupError, value);
    }

    public string ModelProbeStatus
    {
        get => modelProbeStatus;
        private set => SetProperty(ref modelProbeStatus, value);
    }

    public string DeviceCode
    {
        get => deviceCode;
        private set
        {
            if (SetProperty(ref deviceCode, value))
            {
                OnPropertyChanged(nameof(HasDeviceFlowInfo));
            }
        }
    }

    public string VerificationUrl
    {
        get => verificationUrl;
        private set
        {
            if (SetProperty(ref verificationUrl, value))
            {
                OnPropertyChanged(nameof(HasDeviceFlowInfo));
                OnPropertyChanged(nameof(CanOpenVerificationUrl));
            }
        }
    }

    public string ManualCommandGuidance
    {
        get => manualCommandGuidance;
        private set
        {
            if (SetProperty(ref manualCommandGuidance, value))
            {
                OnPropertyChanged(nameof(HasManualGuidance));
            }
        }
    }

    public bool HasDeviceFlowInfo => !string.IsNullOrWhiteSpace(DeviceCode) || !string.IsNullOrWhiteSpace(VerificationUrl);

    public bool CanOpenVerificationUrl => Uri.TryCreate(VerificationUrl, UriKind.Absolute, out _);

    public bool HasManualGuidance => !string.IsNullOrWhiteSpace(ManualCommandGuidance);

    public bool CanStop
    {
        get => canStop;
        private set => SetProperty(ref canStop, value);
    }

    public bool CanRun => IsAuthenticated && !IsBusy && !string.IsNullOrWhiteSpace(Prompt);
    public bool CanApprovePlan => !IsBusy && !IsCurrentPlanApproved;
    public bool CanExecutePlan => !IsBusy && IsCurrentPlanApproved;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var recoveredRuns = await planExecutionService.RecoverRunningRunsAsync(cancellationToken);
        if (recoveredRuns > 0)
        {
            AddTimeline($"Recovered {recoveredRuns} previously running session(s) after restart.");
        }

        await LoadWorkspaceSettingsAsync(cancellationToken);
        await LoadWorkspacePolicyAsync(cancellationToken);
        await RunHealthChecksAsync(cancellationToken);
        await LoadModelsAsync(cancellationToken);
        await RefreshPlanStateAsync(cancellationToken);
        await RefreshConnectorsAsync(cancellationToken);
        await RefreshAuthStatusAsync(cancellationToken);
    }

    public async Task AddWorkspaceAsync(CancellationToken cancellationToken)
    {
        var workspaceId = NormalizeWorkspaceId(NewWorkspaceId);
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            AddTimeline("Workspace ID is empty.");
            return;
        }

        if (Workspaces.Contains(workspaceId, StringComparer.OrdinalIgnoreCase))
        {
            AddTimeline($"Workspace already exists: {workspaceId}");
            return;
        }

        Workspaces.Add(workspaceId);
        NewWorkspaceId = string.Empty;
        await ChangeWorkspaceAsync(workspaceId, cancellationToken);
        AddTimeline($"Added workspace: {workspaceId}");
    }

    public async Task RemoveSelectedWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (Workspaces.Count <= 1)
        {
            AddTimeline("At least one workspace must remain.");
            return;
        }

        var current = Workspaces.FirstOrDefault(x => string.Equals(x, SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            return;
        }

        Workspaces.Remove(current);
        var next = Workspaces.FirstOrDefault() ?? DefaultWorkspaceId;
        await ChangeWorkspaceAsync(next, cancellationToken);
        AddTimeline($"Removed workspace: {current}");
    }

    public async Task ChangeWorkspaceAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeWorkspaceId(workspaceId);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var matched = Workspaces.FirstOrDefault(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)) ?? normalized;
        if (!Workspaces.Contains(matched, StringComparer.OrdinalIgnoreCase))
        {
            Workspaces.Add(matched);
        }

        SelectedWorkspaceId = matched;
        await SaveWorkspaceSettingsAsync(cancellationToken);
        await LoadWorkspacePolicyAsync(cancellationToken);
        await LoadModelsAsync(cancellationToken);
        await RefreshPlanStateAsync(cancellationToken);
        await RefreshConnectorsAsync(cancellationToken);
        AddTimeline($"Switched workspace: {SelectedWorkspaceId}");
    }

    public async Task SaveConnectorAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewConnectorName) || string.IsNullOrWhiteSpace(NewConnectorCommand))
        {
            AddTimeline("Connector name/command is required.");
            return;
        }

        var definition = new ConnectorDefinition(
            NewConnectorName.Trim(),
            NewConnectorCommand.Trim(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new[] { SelectedWorkspaceId });

        await connectorRegistry.SaveAsync(definition, cancellationToken);
        NewConnectorName = string.Empty;
        NewConnectorCommand = string.Empty;
        await RefreshConnectorsAsync(cancellationToken);
        AddTimeline($"Connector saved: {definition.Name}");
    }

    public async Task ConnectConnectorAsync(string connectorName, CancellationToken cancellationToken)
    {
        var all = await connectorRegistry.GetAllAsync(cancellationToken);
        var definition = all.FirstOrDefault(x => string.Equals(x.Name, connectorName, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            AddTimeline($"Connector not found: {connectorName}");
            return;
        }

        var status = await connectorBroker.ConnectAsync(definition, cancellationToken);
        await RefreshConnectorsAsync(cancellationToken);
        AddTimeline(status.Connected
            ? $"Connector connected: {status.Name}"
            : $"[ConnectorError] {status.Name}: {status.LastError}");
    }

    public async Task DisconnectConnectorAsync(string connectorName, CancellationToken cancellationToken)
    {
        await connectorBroker.DisconnectAsync(connectorName, cancellationToken);
        await RefreshConnectorsAsync(cancellationToken);
        AddTimeline($"Connector disconnected: {connectorName}");
    }

    public async Task CreatePlanAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Prompt))
        {
            AddTimeline("Plan creation blocked: prompt is empty.");
            return;
        }

        var plan = await planExecutionService.CreatePlanAsync(SelectedWorkspaceId, Prompt, cancellationToken);
        await RefreshPlanStateAsync(cancellationToken);
        AddTimeline($"Plan created with {plan.Steps.Count} steps.");
    }

    public async Task ApprovePlanAsync(CancellationToken cancellationToken)
    {
        var approved = await planExecutionService.ApproveLatestPlanAsync(SelectedWorkspaceId, cancellationToken);
        await RefreshPlanStateAsync(cancellationToken);
        AddTimeline($"Plan approved ({approved.PlanId}).");
    }

    public async Task ExecutePlanAsync(CancellationToken cancellationToken)
    {
        if (!TryBeginOperation("plan execution", cancellationToken, out var operationToken, out var operationCts))
        {
            AddTimeline($"Operation in progress: {activeOperationName}.");
            return;
        }

        try
        {
            await foreach (var evt in planExecutionService.ExecuteLatestApprovedPlanAsync(SelectedWorkspaceId, operationToken))
            {
                AddTimeline(FormatRunEvent(evt));
            }

            await RefreshPlanStateAsync(operationToken);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            AddTimeline("[Cancelled] Plan execution cancelled.");
        }
        catch (Exception ex)
        {
            AddTimeline($"[PlanExecutionFailed] {ex.Message}");
        }
        finally
        {
            EndOperation(operationCts);
        }
    }

    public async Task ResumeRunAsync(CancellationToken cancellationToken)
    {
        if (!TryBeginOperation("plan resume", cancellationToken, out var operationToken, out var operationCts))
        {
            AddTimeline($"Operation in progress: {activeOperationName}.");
            return;
        }

        try
        {
            await foreach (var evt in planExecutionService.ResumeLatestRunAsync(SelectedWorkspaceId, operationToken))
            {
                AddTimeline(FormatRunEvent(evt));
            }

            await RefreshPlanStateAsync(operationToken);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            AddTimeline("[Cancelled] Plan resume cancelled.");
        }
        catch (Exception ex)
        {
            AddTimeline($"[PlanResumeFailed] {ex.Message}");
        }
        finally
        {
            EndOperation(operationCts);
        }
    }

    public async Task AddFolderGrantAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(NewGrantPath))
        {
            AddTimeline("Grant path is empty.");
            return;
        }

        var fullPath = Path.GetFullPath(NewGrantPath.Trim());
        var current = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);

        if (!Directory.Exists(fullPath))
        {
            AddTimeline($"Grant path does not exist: {fullPath}");
            return;
        }

        if (current.PathGrants.Any(grant => string.Equals(Path.GetFullPath(grant.AbsolutePath), fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            AddTimeline($"Grant already exists: {fullPath}");
            return;
        }

        var updated = current with
        {
            PathGrants = current.PathGrants
                .Append(new PathGrant(fullPath, AllowRead: true, AllowWrite: true, AllowCreate: true))
                .ToArray()
        };

        if (updated.NetworkAllowlist.AllowedHosts.Count == 0)
        {
            updated = networkPolicyService.WithDefaultAllowlist(SelectedWorkspaceId, updated.PathGrants);
        }

        await workspacePolicyStore.SaveAsync(updated, cancellationToken);
        NewGrantPath = string.Empty;
        await LoadWorkspacePolicyAsync(cancellationToken);
        AddTimeline($"Added grant: {fullPath}");
    }

    public async Task RemoveFolderGrantAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var current = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);
        var filtered = current.PathGrants
            .Where(grant => !string.Equals(Path.GetFullPath(grant.AbsolutePath), fullPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (filtered.Length == current.PathGrants.Count)
        {
            return;
        }

        await workspacePolicyStore.SaveAsync(current with { PathGrants = filtered }, cancellationToken);
        await LoadWorkspacePolicyAsync(cancellationToken);
        AddTimeline($"Removed grant: {fullPath}");
    }

    public async Task AddAllowedHostAsync(CancellationToken cancellationToken)
    {
        var host = NormalizeHost(NewAllowedHost);
        if (string.IsNullOrWhiteSpace(host))
        {
            AddTimeline("Allowed host is empty.");
            return;
        }

        var policy = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);
        if (policy.NetworkAllowlist.AllowedHosts.Contains(host))
        {
            AddTimeline($"Host already allowed: {host}");
            return;
        }

        var updatedHosts = new HashSet<string>(policy.NetworkAllowlist.AllowedHosts, StringComparer.OrdinalIgnoreCase)
        {
            host
        };

        await workspacePolicyStore.SaveAsync(policy with { NetworkAllowlist = new NetworkAllowlist(updatedHosts) }, cancellationToken);
        NewAllowedHost = string.Empty;
        await LoadWorkspacePolicyAsync(cancellationToken);
        AddTimeline($"Added allowed host: {host}");
    }

    public async Task RemoveAllowedHostAsync(string host, CancellationToken cancellationToken)
    {
        var normalized = NormalizeHost(host);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        var policy = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);
        var updatedHosts = new HashSet<string>(policy.NetworkAllowlist.AllowedHosts, StringComparer.OrdinalIgnoreCase);
        if (!updatedHosts.Remove(normalized))
        {
            return;
        }

        await workspacePolicyStore.SaveAsync(policy with { NetworkAllowlist = new NetworkAllowlist(updatedHosts) }, cancellationToken);
        await LoadWorkspacePolicyAsync(cancellationToken);
        AddTimeline($"Removed allowed host: {normalized}");
    }

    public async Task ApplyDefaultAllowlistAsync(CancellationToken cancellationToken)
    {
        var policy = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);
        var updated = networkPolicyService.WithDefaultAllowlist(SelectedWorkspaceId, policy.PathGrants);
        await workspacePolicyStore.SaveAsync(updated, cancellationToken);
        await LoadWorkspacePolicyAsync(cancellationToken);
        AddTimeline("Applied default allowlist for workspace.");
    }

    public async Task SaveModelSelectionAsync(CancellationToken cancellationToken)
    {
        await modelCatalogService.SaveModelSelectionAsync(new ModelSelection(SelectedWorkspaceId, SelectedModel), cancellationToken);
        AddTimeline($"Model saved: {SelectedModel}");
    }

    public async Task RefreshAuthStatusAsync(CancellationToken cancellationToken)
    {
        var status = await authService.GetAuthStateAsync(cancellationToken);
        IsAuthenticated = status.IsAuthenticated;

        if (status.IsAuthenticated)
        {
            AuthStatus = $"Logged in as {status.Login ?? "unknown"} ({status.Host ?? "github.com"})";
            AddTimeline(AuthStatus);
            ClearLoginAssistData();
            return;
        }

        AuthStatus = string.IsNullOrWhiteSpace(status.StatusMessage)
            ? "Not logged in to Copilot."
            : $"Not logged in: {status.StatusMessage}";

        AddTimeline(AuthStatus);
    }

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (!TryBeginOperation("login", cancellationToken, out var operationToken, out var operationCts))
        {
            AddTimeline($"Operation in progress: {activeOperationName}.");
            return;
        }

        AddTimeline("Starting Copilot device login...");

        try
        {
            var loginResult = await authService.LoginAsync(operationToken);
            AddTimeline(loginResult.Message);

            DeviceCode = loginResult.DeviceCode ?? string.Empty;
            VerificationUrl = loginResult.VerificationUrl ?? string.Empty;
            ManualCommandGuidance = loginResult.ManualCommandGuidance ?? string.Empty;

            if (HasDeviceFlowInfo)
            {
                AddTimeline($"Device code ready: {DeviceCode}");
                if (!string.IsNullOrWhiteSpace(VerificationUrl))
                {
                    AddTimeline($"Verification URL: {VerificationUrl}");
                }
            }
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            AddTimeline("[Cancelled] Login cancelled.");
        }
        catch (Exception ex)
        {
            AddTimeline($"[LoginFailed] {ex.Message}");
        }
        finally
        {
            EndOperation(operationCts);
        }

        if (!operationToken.IsCancellationRequested)
        {
            await RefreshAuthStatusAsync(operationToken);
        }
    }

    public async Task CopyDeviceCodeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(DeviceCode))
        {
            AddTimeline("No device code available to copy.");
            return;
        }

        await Clipboard.Default.SetTextAsync(DeviceCode);
        AddTimeline("Device code copied to clipboard.");
    }

    public async Task OpenVerificationPageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanOpenVerificationUrl)
        {
            AddTimeline("Verification URL is unavailable.");
            return;
        }

        await networkPolicyGuard.EnsureUrlAllowedAsync(VerificationUrl, cancellationToken);
        await Launcher.Default.OpenAsync(VerificationUrl);
        AddTimeline("Opened verification page in browser.");
    }

    public void DismissDeviceFlowInfo()
    {
        ClearLoginAssistData();
        AddTimeline("Device-flow details dismissed.");
    }

    public async Task ExportDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var destination = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPlane",
            "exports");

        var path = await localLogService.ExportAsync(destination, cancellationToken);
        if (string.IsNullOrWhiteSpace(path))
        {
            AddTimeline("Diagnostics export skipped (local logging disabled or no logs).");
            return;
        }

        AddTimeline($"Diagnostics exported: {path}");
    }

    public Task StopAsync()
    {
        var cts = activeOperationCts;
        if (cts is null)
        {
            AddTimeline("No operation is currently running.");
            return Task.CompletedTask;
        }

        cts.Cancel();
        AddTimeline($"Cancellation requested for {activeOperationName ?? "operation"}.");
        return Task.CompletedTask;
    }

    public async Task ClearSessionAsync(CancellationToken cancellationToken)
    {
        await historyRepository.ClearEntriesAsync(SelectedWorkspaceId, cancellationToken);
        AddTimeline($"Session cleared for workspace: {SelectedWorkspaceId}");
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!CanRun)
        {
            AddTimeline("Run blocked: ensure you are logged in and prompt is not empty.");
            return;
        }

        if (!TryBeginOperation("run", cancellationToken, out var operationToken, out var operationCts))
        {
            AddTimeline($"Operation in progress: {activeOperationName}.");
            return;
        }

        AddTimeline($"Running with model `{SelectedModel}`...");

        try
        {
            var workspaceImages = await TryHandleWorkspaceImageIntentAsync(operationToken);
            if (workspaceImages.Handled)
            {
                AddTimeline($"Assistant: {workspaceImages.Output}");
                return;
            }

            var workspaceListing = await TryHandleWorkspaceListIntentAsync(operationToken);
            if (workspaceListing.Handled)
            {
                AddTimeline($"Assistant: {workspaceListing.Output}");
                return;
            }

            if (Prompt.TrimStart().StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
            {
                var toolOutput = await ExecuteToolPromptAsync(operationToken);
                AddTimeline(toolOutput);
                return;
            }

            var historicalEntries = await historyRepository.GetEntriesAsync(SelectedWorkspaceId, operationToken);
            var promptForExecution = ConversationPromptBuilder.Build(Prompt, historicalEntries);

            await historyRepository.AddEntryAsync(
                new ConversationEntry(Guid.NewGuid().ToString("N"), SelectedWorkspaceId, "user", Prompt, DateTimeOffset.UtcNow),
                operationToken);

            var policy = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, operationToken);
            var attachments = await promptAttachmentResolver.ResolveAsync(Prompt, policy, operationToken);
            if (attachments.Count > 0)
            {
                AddTimeline($"Attached {attachments.Count} workspace file(s).");
            }

            var assistantOutput = await executionService.ExecutePromptAsync(promptForExecution, SelectedModel, attachments, operationToken);
            AddTimeline($"Assistant: {assistantOutput}");

            await historyRepository.AddEntryAsync(
                new ConversationEntry(Guid.NewGuid().ToString("N"), SelectedWorkspaceId, "assistant", assistantOutput, DateTimeOffset.UtcNow),
                operationToken);
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            AddTimeline("[Cancelled] Run cancelled.");
        }
        catch (PolicyViolationException ex)
        {
            AddTimeline($"[PolicyViolation] {ex.Message}");
        }
        catch (Exception ex)
        {
            AddTimeline($"[RunFailed] {ex.Message}");
        }
        finally
        {
            EndOperation(operationCts);
        }
    }

    private async Task<(bool Handled, string Output)> TryHandleWorkspaceListIntentAsync(CancellationToken cancellationToken)
    {
        var promptLower = Prompt.Trim().ToLowerInvariant();
        var asksForFileList = promptLower.Contains("list", StringComparison.Ordinal) &&
                              (promptLower.Contains("file", StringComparison.Ordinal) || promptLower.Contains("files", StringComparison.Ordinal));
        if (!asksForFileList)
        {
            return (false, string.Empty);
        }

        var policy = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);
        if (policy.PathGrants.Count == 0)
        {
            return (true, "No workspace grants configured. Add a folder grant first.");
        }

        var allFiles = new List<string>();
        foreach (var grant in policy.PathGrants)
        {
            if (!grant.AllowRead || string.IsNullOrWhiteSpace(grant.AbsolutePath))
            {
                continue;
            }

            var normalizedPath = Path.GetFullPath(grant.AbsolutePath);
            if (!Directory.Exists(normalizedPath))
            {
                continue;
            }

            var step = new PlanStep(
                Guid.NewGuid().ToString("N"),
                "List workspace files",
                $"tool:search|{normalizedPath}|*",
                SignificantAction: false);

            var output = await agentExecutor.ExecuteStepAsync(step, cancellationToken);
            if (string.IsNullOrWhiteSpace(output) || string.Equals(output.Trim(), "(no matches)", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            allFiles.AddRange(output
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        var distinct = allFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (distinct.Length == 0)
        {
            return (true, "No files found within granted folders.");
        }

        var limited = distinct.Take(200).ToArray();
        var suffix = distinct.Length > limited.Length
            ? $"{Environment.NewLine}...and {distinct.Length - limited.Length} more files."
            : string.Empty;

        var rendered = string.Join(Environment.NewLine, limited) + suffix;
        return (true, rendered);
    }

    private async Task<(bool Handled, string Output)> TryHandleWorkspaceImageIntentAsync(CancellationToken cancellationToken)
    {
        var promptLower = Prompt.Trim().ToLowerInvariant();
        var asksForImages = promptLower.Contains("list", StringComparison.Ordinal) &&
                            (promptLower.Contains("image", StringComparison.Ordinal) || promptLower.Contains("images", StringComparison.Ordinal));
        if (!asksForImages)
        {
            return (false, string.Empty);
        }

        var patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" };
        var imagePaths = await SearchWorkspaceFilesAsync(patterns, cancellationToken);
        if (imagePaths.Length == 0)
        {
            return (true, "No images found within granted folders.");
        }

        var lines = new List<string>
        {
            $"Found {imagePaths.Length} image file(s):"
        };
        lines.AddRange(imagePaths.Take(200));
        if (imagePaths.Length > 200)
        {
            lines.Add($"...and {imagePaths.Length - 200} more images.");
        }

        if (promptLower.Contains("what is in", StringComparison.Ordinal) ||
            promptLower.Contains("tell me what is in", StringComparison.Ordinal) ||
            promptLower.Contains("describe", StringComparison.Ordinal))
        {
            var first = imagePaths[0];
            var readStep = new PlanStep(
                Guid.NewGuid().ToString("N"),
                "Read image extract",
                $"tool:read|{first}",
                SignificantAction: false);

            var extract = await agentExecutor.ExecuteStepAsync(readStep, cancellationToken);
            var preview = string.IsNullOrWhiteSpace(extract) ? "(no extract content)" : extract;
            if (preview.Length > 600)
            {
                preview = preview[..600] + Environment.NewLine + "...[truncated]";
            }

            lines.Add(string.Empty);
            lines.Add($"Preview for {first}:");
            lines.Add(preview);
            lines.Add("Note: pixel-level image understanding is not implemented yet; this is extract-only content.");
        }

        return (true, string.Join(Environment.NewLine, lines));
    }

    private async Task<string[]> SearchWorkspaceFilesAsync(string[] patterns, CancellationToken cancellationToken)
    {
        var policy = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);
        if (policy.PathGrants.Count == 0)
        {
            return [];
        }

        var allFiles = new List<string>();
        foreach (var grant in policy.PathGrants)
        {
            if (!grant.AllowRead || string.IsNullOrWhiteSpace(grant.AbsolutePath))
            {
                continue;
            }

            var normalizedPath = Path.GetFullPath(grant.AbsolutePath);
            if (!Directory.Exists(normalizedPath))
            {
                continue;
            }

            foreach (var pattern in patterns)
            {
                var step = new PlanStep(
                    Guid.NewGuid().ToString("N"),
                    "Search workspace files",
                    $"tool:search|{normalizedPath}|{pattern}",
                    SignificantAction: false);

                var output = await agentExecutor.ExecuteStepAsync(step, cancellationToken);
                if (string.IsNullOrWhiteSpace(output) || string.Equals(output.Trim(), "(no matches)", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                allFiles.AddRange(output
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        return allFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<string> ExecuteToolPromptAsync(CancellationToken cancellationToken)
    {
        var details = Prompt.Trim();
        var step = new PlanStep(Guid.NewGuid().ToString("N"), "Execute tool prompt", details, SignificantAction: true);
        var output = await agentExecutor.ExecuteStepAsync(step, cancellationToken);
        return string.IsNullOrWhiteSpace(output) ? "(no output)" : output;
    }

    private async Task LoadModelsAsync(CancellationToken cancellationToken)
    {
        Models.Clear();
        var availableModels = await modelCatalogService.GetAvailableModelsAsync(cancellationToken);
        foreach (var model in availableModels)
        {
            Models.Add(model.Id);
        }

        var selection = await modelCatalogService.GetModelSelectionAsync(SelectedWorkspaceId, cancellationToken);
        SelectedModel = Models.Any(model => string.Equals(model, selection.ModelId, StringComparison.OrdinalIgnoreCase))
            ? selection.ModelId
            : "gpt-5-mini";
    }

    private async Task LoadWorkspacePolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await workspacePolicyStore.GetAsync(SelectedWorkspaceId, cancellationToken);
        var sorted = policy.PathGrants
            .Select(grant => Path.GetFullPath(grant.AbsolutePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        GrantedFolders.Clear();
        foreach (var path in sorted)
        {
            GrantedFolders.Add(path);
        }

        AllowedHosts.Clear();
        foreach (var host in policy.NetworkAllowlist.AllowedHosts.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            AllowedHosts.Add(host);
        }
    }

    private async Task LoadWorkspaceSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await workspaceSettingsStore.LoadAsync(cancellationToken);

        Workspaces.Clear();
        foreach (var workspaceId in settings.WorkspaceIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            Workspaces.Add(workspaceId);
        }

        if (Workspaces.Count == 0)
        {
            Workspaces.Add(DefaultWorkspaceId);
        }

        SelectedWorkspaceId = Workspaces.FirstOrDefault(x => string.Equals(x, settings.SelectedWorkspaceId, StringComparison.OrdinalIgnoreCase))
            ?? Workspaces[0];
    }

    private async Task RefreshConnectorsAsync(CancellationToken cancellationToken)
    {
        var registered = await connectorRegistry.GetAllAsync(cancellationToken);
        var statuses = await connectorBroker.GetStatusesAsync(cancellationToken);
        var statusMap = statuses.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        ConnectorStatuses.Clear();
        foreach (var connector in registered.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            statusMap.TryGetValue(connector.Name, out var status);
            var connected = status?.Connected ?? false;
            var error = status?.LastError;
            ConnectorStatuses.Add(new ConnectorStatusViewItem(
                connector.Name,
                connector.Command,
                connected ? "Connected" : "Disconnected",
                error));
        }
    }

    private async Task SaveWorkspaceSettingsAsync(CancellationToken cancellationToken)
    {
        await workspaceSettingsStore.SaveAsync(
            new WorkspaceSettings(
                Workspaces.ToArray(),
                SelectedWorkspaceId),
            cancellationToken);
    }

    private static string NormalizeWorkspaceId(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeHost(string? host)
    {
        return host?.Trim().Trim().Trim('/').ToLowerInvariant() ?? string.Empty;
    }

    private async Task RefreshPlanStateAsync(CancellationToken cancellationToken)
    {
        var plan = await planExecutionService.GetLatestPlanAsync(SelectedWorkspaceId, cancellationToken);
        if (plan is null)
        {
            CurrentPlanSummary = "No plan created.";
            IsCurrentPlanApproved = false;
        }
        else
        {
            CurrentPlanSummary = $"Plan {plan.PlanId}: {plan.Steps.Count} steps, risk={plan.RiskLevel}";
            IsCurrentPlanApproved = plan.IsApproved;
        }

        var latestRun = await planExecutionService.GetLatestRunAsync(SelectedWorkspaceId, cancellationToken);
        if (latestRun is null)
        {
            LatestRunStatus = "No run session.";
            return;
        }

        LatestRunStatus = latestRun.Status switch
        {
            RunStatus.Completed => $"Run {latestRun.RunId}: completed",
            RunStatus.Failed => $"Run {latestRun.RunId}: failed ({latestRun.FailureReason ?? "unknown"})",
            RunStatus.Running => $"Run {latestRun.RunId}: running (next step {latestRun.NextStepIndex + 1})",
            _ => $"Run {latestRun.RunId}: {latestRun.Status}"
        };
    }

    private static string FormatRunEvent(RunEvent evt)
    {
        return evt.EventType switch
        {
            RunEventType.RunStarted => $"[RunStarted] {evt.Message}",
            RunEventType.StepStarted => $"[StepStarted:{evt.StepId}] {evt.Message}",
            RunEventType.StepOutput => $"[StepOutput:{evt.StepId}] {evt.Message}",
            RunEventType.StepCompleted => $"[StepCompleted:{evt.StepId}] {evt.Message}",
            RunEventType.PolicyViolation => $"[PolicyViolation:{evt.StepId}] {evt.Message}",
            RunEventType.RunFailed => $"[RunFailed:{evt.StepId}] {evt.Message}",
            RunEventType.RunCompleted => $"[RunCompleted] {evt.Message}",
            _ => $"[{evt.EventType}] {evt.Message}"
        };
    }

    private async Task RunHealthChecksAsync(CancellationToken cancellationToken)
    {
        var report = await healthService.CheckAsync(cancellationToken);

        EffectiveConnection = report.EffectiveCommand;

        CliVersion = string.IsNullOrWhiteSpace(report.CliVersion) ? "n/a" : report.CliVersion;
        ModelProbeStatus = report.ModelProbeSucceeded ? "OK" : "Failed";
        LastStartupError = string.IsNullOrWhiteSpace(report.LastStartupError) ? "None" : report.LastStartupError;

        AddTimeline($"Health check: mode={report.ExecutionMode}, model probe={ModelProbeStatus}");
        if (!string.IsNullOrWhiteSpace(report.LastStartupError))
        {
            AddTimeline($"[StartupError] {report.LastStartupError}");
        }
    }

    private void ClearLoginAssistData()
    {
        DeviceCode = string.Empty;
        VerificationUrl = string.Empty;
        ManualCommandGuidance = string.Empty;
    }

    private bool TryBeginOperation(
        string operationName,
        CancellationToken externalToken,
        out CancellationToken operationToken,
        out CancellationTokenSource? operationCts)
    {
        if (activeOperationCts is not null)
        {
            operationToken = externalToken;
            operationCts = null;
            return false;
        }

        operationCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        activeOperationCts = operationCts;
        activeOperationName = operationName;
        operationToken = operationCts.Token;
        IsBusy = true;
        CanStop = true;
        return true;
    }

    private void EndOperation(CancellationTokenSource? operationCts)
    {
        if (operationCts is null)
        {
            return;
        }

        if (ReferenceEquals(activeOperationCts, operationCts))
        {
            activeOperationCts = null;
            activeOperationName = null;
            CanStop = false;
            IsBusy = false;
        }

        operationCts.Dispose();
    }

    private void AddTimeline(string message)
    {
        _ = localLogService.LogAsync("timeline", message, CancellationToken.None);
        if (MainThread.IsMainThread)
        {
            Timeline.Add(message);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => Timeline.Add(message));
    }

    private bool SetProperty<T>(ref T target, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(target, value))
        {
            return false;
        }

        target = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record ConnectorStatusViewItem(
    string Name,
    string Command,
    string Status,
    string? LastError);
