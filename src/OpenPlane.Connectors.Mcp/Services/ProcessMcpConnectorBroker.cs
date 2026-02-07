using System.Diagnostics;
using OpenPlane.Core.Abstractions;
using OpenPlane.Core.Models;

namespace OpenPlane.Connectors.Mcp.Services;

public sealed class ProcessMcpConnectorBroker : IMcpConnectorBroker, IAsyncDisposable
{
    private readonly SemaphoreSlim mutex = new(1, 1);
    private readonly Dictionary<string, ProcessHandle> processes = new(StringComparer.OrdinalIgnoreCase);

    public async Task<ConnectorStatus> ConnectAsync(ConnectorDefinition definition, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            if (processes.TryGetValue(definition.Name, out var existing) && !existing.Process.HasExited)
            {
                return new ConnectorStatus(definition.Name, Connected: true, LastError: null);
            }

            if (processes.TryGetValue(definition.Name, out existing))
            {
                DisposeHandle(existing);
                processes.Remove(definition.Name);
            }

            var (fileName, args) = ParseCommand(definition.Command);
            var startInfo = new ProcessStartInfo(fileName, args)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            foreach (var pair in definition.EnvironmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ConnectorStatus(definition.Name, false, "Failed to start connector process.");
            }

            var handle = new ProcessHandle(process);
            processes[definition.Name] = handle;

            await Task.Delay(200, cancellationToken);
            if (process.HasExited)
            {
                var err = await process.StandardError.ReadToEndAsync(cancellationToken);
                DisposeHandle(handle);
                processes.Remove(definition.Name);
                return new ConnectorStatus(definition.Name, false, string.IsNullOrWhiteSpace(err) ? "Connector exited immediately." : err.Trim());
            }

            return new ConnectorStatus(definition.Name, true, null);
        }
        catch (Exception ex)
        {
            return new ConnectorStatus(definition.Name, false, ex.Message);
        }
        finally
        {
            mutex.Release();
        }
    }

    public async Task DisconnectAsync(string connectorName, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            if (!processes.TryGetValue(connectorName, out var handle))
            {
                return;
            }

            DisposeHandle(handle);
            processes.Remove(connectorName);
        }
        finally
        {
            mutex.Release();
        }
    }

    public async Task<IReadOnlyList<ConnectorStatus>> GetStatusesAsync(CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            var statuses = new List<ConnectorStatus>();
            var toRemove = new List<string>();

            foreach (var (name, handle) in processes)
            {
                if (handle.Process.HasExited)
                {
                    toRemove.Add(name);
                    statuses.Add(new ConnectorStatus(name, false, $"Exited ({handle.Process.ExitCode})"));
                    continue;
                }

                statuses.Add(new ConnectorStatus(name, true, null));
            }

            foreach (var name in toRemove)
            {
                if (processes.TryGetValue(name, out var handle))
                {
                    DisposeHandle(handle);
                }

                processes.Remove(name);
            }

            return statuses.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally
        {
            mutex.Release();
        }
    }

    private static (string FileName, string Arguments) ParseCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new InvalidOperationException("Connector command is empty.");
        }

        var trimmed = command.Trim();
        var split = trimmed.IndexOf(' ');
        if (split < 0)
        {
            return (trimmed, string.Empty);
        }

        var fileName = trimmed[..split];
        var args = trimmed[(split + 1)..].TrimStart();
        return (fileName, args);
    }

    private static void DisposeHandle(ProcessHandle handle)
    {
        try
        {
            if (!handle.Process.HasExited)
            {
                handle.Process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        handle.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        foreach (var (_, handle) in processes)
        {
            DisposeHandle(handle);
        }

        processes.Clear();
        mutex.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class ProcessHandle(Process process) : IDisposable
    {
        public Process Process { get; } = process;

        public void Dispose()
        {
            Process.Dispose();
        }
    }
}
