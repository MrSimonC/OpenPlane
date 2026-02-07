using OpenPlane.App.ViewModels;

namespace OpenPlane.App;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel viewModel;
    private CancellationTokenSource? pageCts;

    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        BindingContext = this.viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        pageCts?.Cancel();
        pageCts?.Dispose();
        pageCts = new CancellationTokenSource();
        await ExecuteUiActionAsync(
            token => viewModel.InitializeAsync(token),
            "Initialization Error");
    }

    protected override void OnDisappearing()
    {
        pageCts?.Cancel();
        pageCts?.Dispose();
        pageCts = null;
        base.OnDisappearing();
    }

    private async void OnRefreshAuthClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.RefreshAuthStatusAsync(token), "Refresh Auth Error");
    }

    private async void OnAddGrantClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.AddFolderGrantAsync(token), "Add Grant Error");
    }

    private async void OnAddWorkspaceClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.AddWorkspaceAsync(token), "Add Workspace Error");
    }

    private async void OnRemoveWorkspaceClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.RemoveSelectedWorkspaceAsync(token), "Remove Workspace Error");
    }

    private async void OnWorkspaceSelectionChanged(object sender, EventArgs e)
    {
        if (sender is Picker { SelectedItem: string workspaceId })
        {
            await ExecuteUiActionAsync(token => viewModel.ChangeWorkspaceAsync(workspaceId, token), "Workspace Switch Error");
        }
    }

    private async void OnRemoveGrantClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string path })
        {
            await ExecuteUiActionAsync(token => viewModel.RemoveFolderGrantAsync(path, token), "Remove Grant Error");
        }
    }

    private async void OnAddAllowedHostClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.AddAllowedHostAsync(token), "Add Allowed Host Error");
    }

    private async void OnRemoveAllowedHostClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string host })
        {
            await ExecuteUiActionAsync(token => viewModel.RemoveAllowedHostAsync(host, token), "Remove Allowed Host Error");
        }
    }

    private async void OnApplyDefaultAllowlistClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.ApplyDefaultAllowlistAsync(token), "Apply Default Allowlist Error");
    }

    private async void OnSaveConnectorClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.SaveConnectorAsync(token), "Save Connector Error");
    }

    private async void OnConnectConnectorClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string connectorName })
        {
            await ExecuteUiActionAsync(token => viewModel.ConnectConnectorAsync(connectorName, token), "Connect Connector Error");
        }
    }

    private async void OnDisconnectConnectorClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string connectorName })
        {
            await ExecuteUiActionAsync(token => viewModel.DisconnectConnectorAsync(connectorName, token), "Disconnect Connector Error");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.LoginAsync(token), "Login Error");
    }

    private async void OnCopyCodeClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.CopyDeviceCodeAsync(token), "Copy Code Error");
    }

    private async void OnOpenVerificationPageClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.OpenVerificationPageAsync(token), "Verification Page Error");
    }

    private void OnDismissDeviceLoginClicked(object sender, EventArgs e)
    {
        viewModel.DismissDeviceFlowInfo();
    }

    private async void OnSaveModelClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.SaveModelSelectionAsync(token), "Save Model Error");
    }

    private async void OnRunClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.RunAsync(token), "Run Error");
    }

    private async void OnCreatePlanClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.CreatePlanAsync(token), "Create Plan Error");
    }

    private async void OnApprovePlanClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.ApprovePlanAsync(token), "Approve Plan Error");
    }

    private async void OnExecutePlanClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.ExecutePlanAsync(token), "Execute Plan Error");
    }

    private async void OnResumeRunClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.ResumeRunAsync(token), "Resume Run Error");
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        await viewModel.StopAsync();
    }

    private async void OnClearSessionClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.ClearSessionAsync(token), "Clear Session Error");
    }

    private async void OnExportDiagnosticsClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.ExportDiagnosticsAsync(token), "Export Diagnostics Error");
    }

    private async Task ExecuteUiActionAsync(
        Func<CancellationToken, Task> action,
        string errorTitle)
    {
        var token = pageCts?.Token ?? CancellationToken.None;
        try
        {
            await action(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(errorTitle, ex.Message, "OK");
        }
    }
}
