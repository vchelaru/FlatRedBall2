using System;
using System.IO;
using System.Threading;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Watches an .achx folder (recursively) for PNG file changes and raises a debounced
/// <see cref="FolderContentsChanged"/> event suitable for refreshing a file browser panel.
/// </summary>
public sealed class PngFolderWatcher : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _lock = new();

    public event Action? FolderContentsChanged;

    public void Watch(string? folder)
    {
        lock (_lock)
        {
            StopWatcherLocked();

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            _watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };

            _watcher.Created += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnRenamed;
            _watcher.Changed += OnFileSystemEvent;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            StopWatcherLocked();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (!PngFolderScanner.IsPngPath(e.FullPath))
            return;

        ScheduleNotify();
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (!PngFolderScanner.IsPngPath(e.FullPath) && !PngFolderScanner.IsPngPath(e.OldFullPath))
            return;

        ScheduleNotify();
    }

    private void ScheduleNotify()
    {
        lock (_lock)
        {
            _debounceTimer ??= new Timer(_ =>
            {
                FolderContentsChanged?.Invoke();
            }, null, Timeout.Infinite, Timeout.Infinite);

            _debounceTimer.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void StopWatcherLocked()
    {
        if (_watcher is null)
            return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFileSystemEvent;
        _watcher.Deleted -= OnFileSystemEvent;
        _watcher.Renamed -= OnRenamed;
        _watcher.Changed -= OnFileSystemEvent;
        _watcher.Dispose();
        _watcher = null;
    }
}
