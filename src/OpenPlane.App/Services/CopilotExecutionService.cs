using GitHub.Copilot.SDK;

namespace OpenPlane.App.Services;

public interface ICopilotExecutionService
{
    Task<string> ExecutePromptAsync(string prompt, string model, IReadOnlyList<string>? attachmentPaths, CancellationToken cancellationToken);
}

public sealed class CopilotExecutionService : ICopilotExecutionService
{
    private readonly ICopilotClientOptionsFactory optionsFactory;
    private readonly ICopilotExecutionSettingsStore settingsStore;
    private readonly INetworkPolicyGuard networkPolicyGuard;

    public CopilotExecutionService(
        ICopilotClientOptionsFactory optionsFactory,
        ICopilotExecutionSettingsStore settingsStore,
        INetworkPolicyGuard networkPolicyGuard)
    {
        this.optionsFactory = optionsFactory;
        this.settingsStore = settingsStore;
        this.networkPolicyGuard = networkPolicyGuard;
    }

    public async Task<string> ExecutePromptAsync(string prompt, string model, IReadOnlyList<string>? attachmentPaths, CancellationToken cancellationToken)
    {
        await networkPolicyGuard.EnsureDefaultCopilotHostsAllowedAsync(cancellationToken);
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
                    completionSource.TrySetResult(latestAssistantMessage);
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

        var messageOptions = new MessageOptions { Prompt = prompt };
        if (attachmentPaths is { Count: > 0 })
        {
            messageOptions.Attachments = attachmentPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => (UserMessageDataAttachmentsItem)new UserMessageDataAttachmentsItemFile
                {
                    Type = "file",
                    Path = path,
                    DisplayName = Path.GetFileName(path)
                })
                .ToList();
        }

        await session.SendAsync(messageOptions);
        var output = await completionSource.Task.WaitAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(output) ? "No assistant output received." : output.Trim();
    }
}
