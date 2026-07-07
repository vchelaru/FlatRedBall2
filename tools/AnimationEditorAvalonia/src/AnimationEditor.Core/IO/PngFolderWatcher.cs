using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Watches an .achx folder (recursively) for PNG file changes and raises a debounced
/// <see cref="FolderContentsChanged"/> event suitable for refreshing a file browser panel.
/// The event carries the set of PNG paths that changed since the last fire, so a listener can
/// invalidate exactly those entries in a thumbnail cache before rebuilding.
/// </summary>
public sealed class PngFolderWatcher : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _lock = new();

    // Absolute paths of PNGs touched since the last debounce fire (created/changed/deleted/renamed).
    private readonly HashSet<string> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised (debounced) after PNG changes settle, with the absolute paths that changed.</summary>
    public event Action<IReadOnlyCollection<string>>? FolderContentsChanged;

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

        ScheduleNotify(e.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        // Report both endpoints: the old name must be evicted from caches, the new one shown.
        if (PngFolderScanner.IsPngPath(e.OldFullPath))
            ScheduleNotify(e.OldFullPath);
        if (PngFolderScanner.IsPngPath(e.FullPath))
            ScheduleNotify(e.FullPath);
    }

    private void ScheduleNotify(string changedPath)
    {
        lock (_lock)
        {
            _pendingChanges.Add(changedPath);

            _debounceTimer ??= new Timer(_ =>
            {
                string[] changed;
                lock (_lock)
                {
                    changed = _pendingChanges.ToArray();
                    _pendingChanges.Clear();
                }
                FolderContentsChanged?.Invoke(changed);
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
