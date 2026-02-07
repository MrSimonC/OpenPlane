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

    private async void OnRemoveGrantClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string path })
        {
            await ExecuteUiActionAsync(token => viewModel.RemoveFolderGrantAsync(path, token), "Remove Grant Error");
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
