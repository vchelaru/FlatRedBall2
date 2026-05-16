using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AnimationEditor.Core.HotReload
{
    /// <summary>
    /// <see cref="FileSystemWatcher"/>-based implementation of <see cref="IHotReloadWatcher"/>.
    /// Fires events on background threads; consumers must marshal to the UI thread.
    /// </summary>
    public sealed class HotReloadWatcher : IHotReloadWatcher
    {
        private readonly FileChangeCoalescer _coalescer;
        private readonly Timer _flushTimer;
        private readonly object _lock = new();

        // Directory path → watcher
        private readonly Dictionary<string, FileSystemWatcher> _watchers =
            new(StringComparer.OrdinalIgnoreCase);

        private string? _achxPath;
        private readonly HashSet<string> _watchedPngPaths =
            new(StringComparer.OrdinalIgnoreCase);

        public event Action<string>? AchxChangedOnDisk;
        public event Action<string>? PngChangedOnDisk;
        public event Action<string>? AchxDeletedOnDisk;

        public bool IsEnabled { get; set; } = true;

        public HotReloadWatcher()
        {
            _coalescer = new FileChangeCoalescer();
            _flushTimer = new Timer(_ => FlushCoalescer(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void StartWatching(string achxPath, IEnumerable<string> pngPaths)
        {
            StopWatching();

            lock (_lock)
            {
                _achxPath = achxPath.Replace('\\', '/');
                _watchedPngPaths.Clear();
                foreach (var p in pngPaths)
                    _watchedPngPaths.Add(p.Replace('\\', '/'));

                var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var achxDir = Path.GetDirectoryName(_achxPath);
                if (!string.IsNullOrEmpty(achxDir) && Directory.Exists(achxDir))
                    dirs.Add(achxDir);

                foreach (var png in _watchedPngPaths)
                {
                    var dir = Path.GetDirectoryName(png);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        dirs.Add(dir);
                }

                foreach (var dir in dirs)
                    AddWatcher(dir);
            }

            _flushTimer.Change(100, 100);
        }

        public void UpdatePngList(IEnumerable<string> newPngPaths)
        {
            lock (_lock)
            {
                var newSet = new HashSet<string>(
                    newPngPaths.Select(p => p.Replace('\\', '/')),
                    StringComparer.OrdinalIgnoreCase);
                var (added, removed) = ReferencedFileDiff.Diff(_watchedPngPaths, newSet);

                foreach (var p in added)   _watchedPngPaths.Add(p);
                foreach (var p in removed) _watchedPngPaths.Remove(p);

                // Add watchers for newly-referenced directories
                foreach (var png in added)
                {
                    var dir = Path.GetDirectoryName(png);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) &&
                        !_watchers.ContainsKey(dir))
                        AddWatcher(dir);
                }

                // Remove watchers for directories no longer needed
                var achxDir = _achxPath != null ? Path.GetDirectoryName(_achxPath) : null;
                var stillNeeded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(achxDir)) stillNeeded.Add(achxDir);
                foreach (var p in _watchedPngPaths)
                {
                    var dir = Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(dir)) stillNeeded.Add(dir);
                }

                var toRemove = _watchers.Keys.Where(d => !stillNeeded.Contains(d)).ToList();
                foreach (var d in toRemove)
                {
                    _watchers[d].Dispose();
                    _watchers.Remove(d);
                }
            }
        }

        public void StopWatching()
        {
            _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);

            lock (_lock)
            {
                foreach (var w in _watchers.Values) w.Dispose();
                _watchers.Clear();
                _achxPath = null;
                _watchedPngPaths.Clear();
            }
        }

        public void RecordOwnSave(string filePath)
        {
            _coalescer.RecordOwnSave(filePath,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private void AddWatcher(string directory)
        {
            if (_watchers.ContainsKey(directory)) return;

            var fsw = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
            };

            long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            fsw.Changed += (_, e) =>
            {
                if (!IsEnabled) return;
                _coalescer.Record(e.FullPath, WatcherChangeType.Modified, Now());
            };
            fsw.Created += (_, e) =>
            {
                if (!IsEnabled) return;
                _coalescer.Record(e.FullPath, WatcherChangeType.Created, Now());
            };
            fsw.Deleted += (_, e) =>
            {
                if (!IsEnabled) return;
                _coalescer.Record(e.FullPath, WatcherChangeType.Deleted, Now());
            };
            fsw.Renamed += (_, e) =>
            {
                if (!IsEnabled) return;
                // Renamed is another form of atomic-write on some tools
                _coalescer.Record(e.OldFullPath, WatcherChangeType.Deleted, Now());
                _coalescer.Record(e.FullPath, WatcherChangeType.Created, Now());
            };

            _watchers[directory] = fsw;
        }

        private void FlushCoalescer()
        {
            if (!IsEnabled) return;

            string? achxPath;
            HashSet<string> pngPaths;

            lock (_lock)
            {
                achxPath = _achxPath;
                pngPaths = new HashSet<string>(_watchedPngPaths, StringComparer.OrdinalIgnoreCase);
            }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var events = _coalescer.Flush(nowMs);

            foreach (var (path, type) in events)
            {
                var normalizedPath = path.Replace('\\', '/');
                bool isAchx = achxPath != null &&
                    string.Equals(normalizedPath, achxPath, StringComparison.OrdinalIgnoreCase);
                bool isPng = pngPaths.Contains(normalizedPath);

                if (!isAchx && !isPng) continue;

                if (isAchx)
                {
                    if (type == WatcherChangeType.Deleted)
                        AchxDeletedOnDisk?.Invoke(normalizedPath);
                    else
                        AchxChangedOnDisk?.Invoke(normalizedPath);
                }
                else if (isPng)
                {
                    if (type != WatcherChangeType.Deleted)
                        PngChangedOnDisk?.Invoke(normalizedPath);
                    // PNG deleted: future frames referencing it will just show as missing
                }
            }
        }

        public void Dispose()
        {
            _flushTimer.Dispose();
            lock (_lock)
            {
                foreach (var w in _watchers.Values) w.Dispose();
                _watchers.Clear();
            }
        }
    }
}
