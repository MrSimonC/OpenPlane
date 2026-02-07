using GitHub.Copilot.SDK;
using System.Runtime.InteropServices;

namespace OpenPlane.App.Services;

public interface ICopilotClientOptionsFactory
{
    CopilotClientOptions Create(CopilotExecutionSettings settings);
    string ResolvedCliPath { get; }
    IReadOnlyList<string> ResolvedCliArgs { get; }
    string DisplayCommand { get; }
}

public sealed class CopilotClientOptionsFactory : ICopilotClientOptionsFactory
{
    private readonly string resolvedCliPath;
    private readonly string workingDirectory;

    public CopilotClientOptionsFactory()
    {
        var resolved = ResolveCliInvocation();
        resolvedCliPath = resolved.CliPath;
        workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string ResolvedCliPath => resolvedCliPath;
    public IReadOnlyList<string> ResolvedCliArgs => [];
    public string DisplayCommand => string.IsNullOrWhiteSpace(resolvedCliPath)
        ? "(bundled Copilot CLI not found)"
        : resolvedCliPath;

    public CopilotClientOptions Create(CopilotExecutionSettings settings)
    {
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            throw new InvalidOperationException(
                "Bundled Copilot CLI not found. This build is configured for bundled CLI only. " +
                "Ensure the app output includes runtimes/<rid>/native/copilot.");
        }

        return new CopilotClientOptions
        {
            AutoStart = true,
            UseLoggedInUser = true,
            CliPath = resolvedCliPath,
            Cwd = workingDirectory
        };
    }

    private static (string CliPath, string[] CliArgs) ResolveCliInvocation()
    {
        foreach (var bundledPath in ResolveBundledPaths())
        {
            if (File.Exists(bundledPath))
            {
                return (bundledPath, []);
            }
        }

        return (string.Empty, []);
    }

    private static IEnumerable<string> ResolveBundledPaths()
    {
        var executableName = OperatingSystem.IsWindows() ? "copilot.exe" : "copilot";
        var runtimeId = RuntimeInformation.RuntimeIdentifier;
        var ridCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(runtimeId))
        {
            ridCandidates.Add(runtimeId);
        }

        if (runtimeId.StartsWith("maccatalyst-", StringComparison.OrdinalIgnoreCase))
        {
            ridCandidates.Add(runtimeId.Replace("maccatalyst-", "osx-", StringComparison.OrdinalIgnoreCase));
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            ridCandidates.Add(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64");
        }
        else if (OperatingSystem.IsLinux())
        {
            ridCandidates.Add(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64");
        }
        else if (OperatingSystem.IsWindows())
        {
            ridCandidates.Add(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64");
        }

        var baseDirectories = EnumerateBaseDirectories(AppContext.BaseDirectory).ToArray();
        foreach (var baseDirectory in baseDirectories)
        {
            foreach (var rid in ridCandidates)
            {
                yield return Path.Combine(baseDirectory, "runtimes", rid, "native", executableName);
            }
        }
    }

    private static IEnumerable<string> EnumerateBaseDirectories(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            yield break;
        }

        var current = Path.GetFullPath(directory);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 6 && !string.IsNullOrWhiteSpace(current) && seen.Add(current); i++)
        {
            yield return current;
            current = Path.GetDirectoryName(current) ?? string.Empty;
        }
    }

}
