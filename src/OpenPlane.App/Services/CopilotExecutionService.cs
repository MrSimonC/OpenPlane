using GitHub.Copilot.SDK;

namespace OpenPlane.App.Services;

public interface ICopilotExecutionService
{
    Task<string> ExecutePromptAsync(string prompt, string model, CancellationToken cancellationToken);
}

public sealed class CopilotExecutionService : ICopilotExecutionService
{
    private readonly ICopilotClientOptionsFactory optionsFactory;
    private readonly ICopilotExecutionSettingsStore settingsStore;

    public CopilotExecutionService(ICopilotClientOptionsFactory optionsFactory, ICopilotExecutionSettingsStore settingsStore)
    {
        this.optionsFactory = optionsFactory;
        this.settingsStore = settingsStore;
    }

    public async Task<string> ExecutePromptAsync(string prompt, string model, CancellationToken cancellationToken)
    {
        var settings = await settingsStore.LoadAsync(cancellationToken);
        await using var client = new CopilotClient(optionsFactory.Create(settings));

        await client.StartAsync();

        var authStatus = await client.GetAuthStatusAsync();
        if (!authStatus.IsAuthenticated)
        {
            throw new InvalidOperationException("Not logged into Copilot. Use the Login button first.");
        }

        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            Streaming = false
        });

        var completionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        string latestAssistantMessage = string.Empty;

        using var subscription = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent messageEvent when !string.IsNullOrWhiteSpace(messageEvent.Data.Content):
                    latestAssistantMessage = messageEvent.Data.Content;
                    break;
                case SessionErrorEvent errorEvent:
                    completionSource.TrySetException(new InvalidOperationException(errorEvent.Data.Message));
                    break;
                case SessionIdleEvent:
                    completionSource.TrySetResult(latestAssistantMessage);
                    break;
            }
        });

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await session.AbortAsync();
                }
                catch
                {
                }
                finally
                {
                    completionSource.TrySetCanceled(cancellationToken);
                }
            });
        });

        await session.SendAsync(new MessageOptions { Prompt = prompt });
        var output = await completionSource.Task.WaitAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(output) ? "No assistant output received." : output.Trim();
    }
}
