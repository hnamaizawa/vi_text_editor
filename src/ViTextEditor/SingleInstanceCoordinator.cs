using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ViTextEditor;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _serverTask;
    private bool _disposed;

    public SingleInstanceCoordinator()
    {
        var sessionKey = $"{Environment.UserDomainName}\\{Environment.UserName}|{Process.GetCurrentProcess().SessionId}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionKey)))[..16];
        var mutexName = $"Local\\ViTextEditor.SingleInstance.{hash}";
        _pipeName = $"ViTextEditor.SingleInstance.{hash}";
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    public void StartServer(Action<IReadOnlyList<string>> onPathsReceived)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsPrimary) throw new InvalidOperationException("Only the primary instance can host the forwarding pipe.");
        if (_serverTask is not null) return;

        _serverTask = Task.Run(() => RunServerAsync(onPathsReceived, _cancellation.Token));
    }

    public bool SendPathsToPrimary(IEnumerable<string> paths, TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsPrimary) return false;

        var payload = JsonSerializer.Serialize(paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Where(path => path is not null)
            .Cast<string>()
            .ToArray());

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var remaining = timeout - stopwatch.Elapsed;
            var connectTimeout = Math.Max(100, Math.Min(500, (int)remaining.TotalMilliseconds));
            try
            {
                using var client = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.None);
                client.Connect(connectTimeout);
                using var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, leaveOpen: false)
                {
                    AutoFlush = true
                };
                writer.WriteLine(payload);
                return true;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(75);
            }
            catch (IOException)
            {
                Thread.Sleep(75);
            }
        }

        return false;
    }

    private async Task RunServerAsync(Action<IReadOnlyList<string>> onPathsReceived, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: false);
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                {
                    onPathsReceived(Array.Empty<string>());
                    continue;
                }

                var paths = JsonSerializer.Deserialize<string[]>(line) ?? Array.Empty<string>();
                onPathsReceived(paths);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                // Ignore malformed forwarding payloads and keep the primary instance alive.
            }
        }
    }

    private static string? NormalizePath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();

        if (IsPrimary)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _mutex.Dispose();
    }
}
