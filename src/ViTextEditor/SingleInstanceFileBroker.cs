using System.IO.Pipes;
using System.Diagnostics;
using System.Text.Json;

namespace ViTextEditor;

internal sealed class SingleInstanceFileBroker : IDisposable
{
    private const string MutexName = @"Local\ViTextEditor.SingleInstance.v1";
    private static readonly string PipeName = $"ViTextEditor.SingleInstance.v1.{Process.GetCurrentProcess().SessionId}";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenTask;

    private SingleInstanceFileBroker(Mutex mutex, bool isPrimary)
    {
        _mutex = mutex;
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public static SingleInstanceFileBroker Acquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return new SingleInstanceFileBroker(mutex, createdNew);
    }

    public void StartListening(Action<IReadOnlyList<string>> onFilesReceived)
    {
        if (!IsPrimary) throw new InvalidOperationException("Only the primary instance can receive files.");
        if (_listenTask is not null) throw new InvalidOperationException("The file receiver is already running.");

        _listenTask = Task.Run(() => ListenAsync(onFilesReceived, _cancellation.Token));
    }

    public static bool ForwardFiles(IEnumerable<string> paths, TimeSpan timeout)
    {
        var fullPaths = NormalizeExistingPaths(paths);
        var deadline = DateTime.UtcNow + timeout;

        do
        {
            try
            {
                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);
                pipe.Connect(250);
                using var writer = new StreamWriter(pipe) { AutoFlush = true };
                writer.WriteLine(JsonSerializer.Serialize(fullPaths));
                return true;
            }
            catch (TimeoutException) { }
            catch (IOException) { }

            Thread.Sleep(50);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    internal static string[] NormalizeExistingPaths(IEnumerable<string> paths)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath) && seen.Add(fullPath)) normalized.Add(fullPath);
            }
            catch
            {
                // Invalid paths are ignored here. The primary instance remains alive
                // and valid files in the same shell request are still delivered.
            }
        }
        return normalized.ToArray();
    }

    private static async Task ListenAsync(Action<IReadOnlyList<string>> onFilesReceived, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(pipe);
                var payload = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(payload)) continue;

                var paths = JsonSerializer.Deserialize<string[]>(payload) ?? [];
                onFilesReceived(NormalizeExistingPaths(paths));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                // A client may exit while writing. Recreate the pipe and keep serving.
            }
            catch (JsonException)
            {
                // Ignore malformed messages without terminating the primary instance.
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (IsPrimary)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { }
        }
        _mutex.Dispose();
        _cancellation.Dispose();
    }
}
