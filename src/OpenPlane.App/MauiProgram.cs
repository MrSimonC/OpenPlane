using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using OpenPlane.App.Services;
using OpenPlane.App.ViewModels;
using OpenPlane.Connectors.Mcp.Services;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Services;
using OpenPlane.Storage.Services;

namespace OpenPlane.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif
		builder.Services.AddSingleton<ICopilotClientOptionsFactory, CopilotClientOptionsFactory>();
		builder.Services.AddSingleton<ICopilotExecutionSettingsStore>(_ => new JsonCopilotExecutionSettingsStore("OpenPlane"));
		builder.Services.AddSingleton<ICopilotHealthService, CopilotHealthService>();
		builder.Services.AddSingleton<ICopilotModelProvider, CopilotModelProvider>();
		builder.Services.AddSingleton<ICopilotAuthService, CopilotAuthService>();
		builder.Services.AddSingleton<ICopilotExecutionService, CopilotExecutionService>();
		builder.Services.AddSingleton<IModelSelectionStore>(_ => new JsonModelSelectionStore("OpenPlane"));
		builder.Services.AddSingleton<IWorkspacePolicyStore>(_ => new JsonWorkspacePolicyStore("OpenPlane"));
		builder.Services.AddSingleton<EncryptionService>(_ => new EncryptionService("OpenPlane"));
		builder.Services.AddSingleton<IHistoryRepository>(provider => new EncryptedHistoryRepository(provider.GetRequiredService<EncryptionService>(), "OpenPlane"));
		builder.Services.AddSingleton<IConnectorRegistry, InMemoryConnectorRegistry>();
		builder.Services.AddSingleton<IMcpConnectorBroker, InMemoryMcpConnectorBroker>();
		builder.Services.AddSingleton<IPlannerService, PlannerService>();
		builder.Services.AddSingleton<IApprovalService, ApprovalService>();
		builder.Services.AddSingleton<IFileToolService, FileToolService>();
		builder.Services.AddSingleton<IAgentExecutor, InlineAgentExecutor>();
		builder.Services.AddSingleton<IRunOrchestrator, RunOrchestrator>();
		builder.Services.AddSingleton<IAccessPolicyService, AccessPolicyService>();
		builder.Services.AddSingleton<INetworkPolicyService, NetworkPolicyService>();
		builder.Services.AddSingleton<IModelCatalogService, ModelCatalogService>();
		builder.Services.AddTransient<MainPageViewModel>();
		builder.Services.AddTransient<MainPage>();

		return builder.Build();
	}
}
