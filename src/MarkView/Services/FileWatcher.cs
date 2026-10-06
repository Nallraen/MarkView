using System.IO;
using System.Windows.Threading;

namespace MarkView.Services;

/// <summary>
/// Watches one file and calls back on the UI thread, debounced, after it was written, created, deleted or renamed.
/// Must be created and disposed on the UI thread.
/// </summary>
public sealed class FileWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public FileWatcher(string path, Action onChanged)
    {
        _timer = new DispatcherTimer { Interval = Debounce };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            onChanged();
        };

        // Watch the folder filtered on the name: this also catches editors that save by delete + rename.
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
        };
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemEvent;
        _watcher.Error += (_, e) => Log.Error(e.GetException(), $"File watcher error on {path}");
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _watcher.Dispose();
    }

    // Raised on a thread-pool thread.
    private void OnFileSystemEvent(object sender, FileSystemEventArgs e) => _timer.Dispatcher.BeginInvoke(Restart);

    private void Restart()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Start();
    }
}
