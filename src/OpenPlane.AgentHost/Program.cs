using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;
using OpenPlane.Core.Services;

var executor = new InlineAgentExecutor();
await RunAsync(executor, args, CancellationToken.None);

static async Task RunAsync(IAgentExecutor executor, string[] args, CancellationToken cancellationToken)
{
    var title = args.Length > 0 ? args[0] : "No step title provided";
    var details = args.Length > 1 ? args[1] : "No details provided";
    var step = new PlanStep(Guid.NewGuid().ToString("N"), title, details, SignificantAction: true);

    var output = await executor.ExecuteStepAsync(step, cancellationToken);
    Console.WriteLine(output);
}
