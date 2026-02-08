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

    private async void OnGrantDropAreaDragOver(object sender, DragEventArgs e)
    {
        var droppedFolder = await TryExtractDroppedFolderAsync(e.Data);
        e.AcceptedOperation = droppedFolder is null
            ? DataPackageOperation.None
            : DataPackageOperation.Copy;
    }

    private async void OnGrantDropAreaDrop(object sender, DropEventArgs e)
    {
        var droppedFolder = await TryExtractDroppedFolderAsync(e.Data);
        if (string.IsNullOrWhiteSpace(droppedFolder))
        {
            await DisplayAlertAsync("Invalid Drop", "Only folders can be dropped here.", "OK");
            return;
        }

        await ExecuteUiActionAsync(token => viewModel.AddFolderGrantPathAsync(droppedFolder, token), "Add Grant Error");
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

    private void OnToggleWorkspaceClicked(object sender, EventArgs e)
    {
        viewModel.ToggleWorkspacePanel();
    }

    private void OnToggleAuthClicked(object sender, EventArgs e)
    {
        viewModel.ToggleAuthPanel();
    }

    private void OnToggleMcpClicked(object sender, EventArgs e)
    {
        viewModel.ToggleMcpPanel();
    }

    private void OnToggleNetworkClicked(object sender, EventArgs e)
    {
        viewModel.ToggleNetworkPanel();
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

    private async void OnStopClicked(object sender, EventArgs e)
    {
        await viewModel.StopAsync();
    }

    private async void OnClearSessionClicked(object sender, EventArgs e)
    {
        await ExecuteUiActionAsync(token => viewModel.ClearSessionAsync(token), "Clear Session Error");
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

    private static async Task<string?> TryExtractDroppedFolderAsync(object? data)
    {
        if (data is null)
        {
            return null;
        }

        if (data is DataPackage package)
        {
            var directTextPath = TryParseDirectoryPathFromText(package.Text);
            if (directTextPath is not null)
            {
                return directTextPath;
            }

            var viewTextFromPackage = await package.View.GetTextAsync();
            var viewTextPathFromPackage = TryParseDirectoryPathFromText(viewTextFromPackage);
            if (viewTextPathFromPackage is not null)
            {
                return viewTextPathFromPackage;
            }

            var packagePath = TryExtractDirectoryPathFromUnknown(package.Properties);
            if (packagePath is not null)
            {
                return packagePath;
            }
        }

        if (data is DataPackageView view)
        {
            var viewText = await view.GetTextAsync();
            var viewTextPath = TryParseDirectoryPathFromText(viewText);
            if (viewTextPath is not null)
            {
                return viewTextPath;
            }

            var viewPath = TryExtractDirectoryPathFromUnknown(view.Properties);
            if (viewPath is not null)
            {
                return viewPath;
            }
        }

        return null;
    }

    private static string? TryParseDirectoryPathFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var tokens = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var normalized = token;
            if (normalized.StartsWith("file://", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            {
                normalized = uri.LocalPath;
            }

            var path = TryNormalizeDirectoryPath(normalized);
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static string? TryNormalizeDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim());
            return Directory.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractDirectoryPathFromUnknown(object? value)
    {
        return TryExtractDirectoryPathFromUnknown(value, depth: 0);
    }

    private static string? TryExtractDirectoryPathFromUnknown(object? value, int depth)
    {
        if (value is null || depth > 4)
        {
            return null;
        }

        if (value is string text)
        {
            return TryParseDirectoryPathFromText(text);
        }

        if (value is Uri uri)
        {
            return TryNormalizeDirectoryPath(uri.LocalPath);
        }

        if (value is IEnumerable<KeyValuePair<string, object>> dict)
        {
            foreach (var pair in dict)
            {
                var keyPath = TryExtractDirectoryPathFromUnknown(pair.Key, depth + 1);
                if (keyPath is not null)
                {
                    return keyPath;
                }

                var valuePath = TryExtractDirectoryPathFromUnknown(pair.Value, depth + 1);
                if (valuePath is not null)
                {
                    return valuePath;
                }
            }

            return null;
        }

        if (value is System.Collections.IEnumerable list && value is not string)
        {
            foreach (var item in list)
            {
                var itemPath = TryExtractDirectoryPathFromUnknown(item, depth + 1);
                if (itemPath is not null)
                {
                    return itemPath;
                }
            }
        }

        return TryParseDirectoryPathFromText(value.ToString());
    }
}
