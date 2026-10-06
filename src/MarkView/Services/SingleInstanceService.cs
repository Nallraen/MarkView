using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;

namespace MarkView.Services;

/// <summary>Single-instance guard (Mutex) + named pipe forwarding command-line paths to the primary instance.</summary>
public sealed class SingleInstanceService : IDisposable
{
    private const int AsfwAny = -1;
    private const int ConnectTimeoutMs = 2000;

    // Per user: the SID keeps two Windows sessions of different users independent (pipe names are machine-wide).
    private static readonly string InstanceId = $"MarkView.SingleInstance.{WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName}";

    private readonly CancellationTokenSource _cancellation = new();
    private Mutex? _mutex;
    private Task? _listenTask;

    /// <summary>Raised on the UI thread when another MarkView process forwarded file paths.</summary>
    public event EventHandler<string[]>? FilesReceived;

    /// <summary>
    /// Returns true when this process is the primary instance (and starts listening for forwarded paths).
    /// Otherwise forwards <paramref name="args"/> to the primary instance, asks it to come to the foreground, and returns false.
    /// </summary>
    public bool TryAcquire(string[] args)
    {
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{InstanceId}", out var owned);
        if (!owned)
        {
            try
            {
                owned = _mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous primary crashed: the mutex is now ours.
                owned = true;
            }
        }

        if (owned)
        {
            _listenTask = ListenAsync(_cancellation.Token);
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        // If the primary cannot be reached, run as an independent instance rather than lose the user's request.
        return !TryForward(args);
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        // The loop only awaits cancellable pipe operations (ConfigureAwait(false)), so this wait is short and cannot deadlock.
        _listenTask?.Wait(TimeSpan.FromSeconds(1));
        _cancellation.Dispose();
        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }
    }

    private static bool TryForward(string[] args)
    {
        try
        {
            // Relative paths are resolved against this process's current directory, not the primary's.
            var paths = args.Select(ToFullPath).ToArray();
            AllowSetForegroundWindow(AsfwAny);

            using var client = new NamedPipeClientStream(".", InstanceId, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(ConnectTimeoutMs);
            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            // Even with zero paths the connection itself tells the primary to come to the front.
            foreach (var path in paths)
            {
                writer.WriteLine(path);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Cannot forward the command line to the running instance");
            return false;
        }
    }

    private static string ToFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Forward as-is: the primary reports the invalid path to the user.
            return path;
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    InstanceId, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var paths = new List<string>();
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    if (line.Length > 0)
                    {
                        paths.Add(line);
                    }
                }

                Application.Current?.Dispatcher.BeginInvoke(() => FilesReceived?.Invoke(this, [.. paths]));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Background loop: a failure must not kill it, nor surface as a crash when Dispose waits for it.
                Log.Error(ex, "Single-instance pipe error");
                try
                {
                    // Avoid a hot loop if the pipe keeps failing.
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
