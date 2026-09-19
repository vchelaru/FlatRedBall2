using AnimationEditor.Core.Paths;
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
        private string? _tiledSyncPath;
        private readonly HashSet<string> _watchedPngPaths =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _watchedTsxPaths =
            new(StringComparer.OrdinalIgnoreCase);

        public event Action<string>? AchxChangedOnDisk;
        public event Action<string>? PngChangedOnDisk;
        public event Action<string>? AchxDeletedOnDisk;
        public event Action<string>? TiledSyncChangedOnDisk;
        public event Action<string>? AssociatedTsxChangedOnDisk;

        public bool IsEnabled { get; set; } = true;

        public HotReloadWatcher()
        {
            _coalescer = new FileChangeCoalescer();
            _flushTimer = new Timer(_ => FlushCoalescer(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void StartWatching(string achxPath, IEnumerable<string> pngPaths, IEnumerable<string> tsxPaths)
        {
            StopWatching();

            lock (_lock)
            {
                _achxPath = Canonicalize(achxPath);
                // .tiledsync is always the achx's own extension swapped -- same convention as
                // IoManager.GetTiledSyncCompanionFileFor -- so no separate path needs passing in.
                _tiledSyncPath = string.IsNullOrEmpty(achxPath)
                    ? null
                    : Canonicalize(Path.ChangeExtension(_achxPath, ".tiledsync"));

                _watchedPngPaths.Clear();
                foreach (var p in pngPaths)
                    _watchedPngPaths.Add(Canonicalize(p));

                _watchedTsxPaths.Clear();
                foreach (var t in tsxPaths)
                    _watchedTsxPaths.Add(Canonicalize(t));

                foreach (var dir in ComputeWatchedDirectories())
                    AddWatcher(dir);
            }

            _flushTimer.Change(100, 100);
        }

        public void UpdatePngList(IEnumerable<string> newPngPaths)
        {
            lock (_lock)
            {
                var newSet = new HashSet<string>(
                    newPngPaths.Select(Canonicalize),
                    StringComparer.OrdinalIgnoreCase);
                var (added, removed) = ReferencedFileDiff.Diff(_watchedPngPaths, newSet);

                foreach (var p in added)   _watchedPngPaths.Add(p);
                foreach (var p in removed) _watchedPngPaths.Remove(p);

                SyncWatchersToCurrentDirectories();
            }
        }

        public void UpdateAssociatedTsxPaths(IEnumerable<string> newTsxPaths)
        {
            lock (_lock)
            {
                var newSet = new HashSet<string>(
                    newTsxPaths.Select(Canonicalize),
                    StringComparer.OrdinalIgnoreCase);
                var (added, removed) = ReferencedFileDiff.Diff(_watchedTsxPaths, newSet);

                foreach (var p in added)   _watchedTsxPaths.Add(p);
                foreach (var p in removed) _watchedTsxPaths.Remove(p);

                SyncWatchersToCurrentDirectories();
            }
        }

        /// <summary>Every directory that must currently be watched: the achx's own directory
        /// (also home to <c>.tiledsync</c>), plus every watched PNG's and .tsx's directory.
        /// Caller must hold <see cref="_lock"/>.</summary>
        private HashSet<string> ComputeWatchedDirectories()
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var achxDir = _achxPath != null ? Path.GetDirectoryName(_achxPath) : null;
            if (!string.IsNullOrEmpty(achxDir) && Directory.Exists(achxDir))
                dirs.Add(achxDir);

            foreach (var path in _watchedPngPaths.Concat(_watchedTsxPaths))
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    dirs.Add(dir);
            }

            return dirs;
        }

        /// <summary>Adds watchers for any newly-needed directory and disposes watchers for any
        /// directory no longer referenced by the achx, a PNG, or a .tsx. Caller must hold
        /// <see cref="_lock"/>.</summary>
        private void SyncWatchersToCurrentDirectories()
        {
            var stillNeeded = ComputeWatchedDirectories();

            foreach (var dir in stillNeeded)
                if (!_watchers.ContainsKey(dir))
                    AddWatcher(dir);

            var toRemove = _watchers.Keys.Where(d => !stillNeeded.Contains(d)).ToList();
            foreach (var d in toRemove)
            {
                _watchers[d].Dispose();
                _watchers.Remove(d);
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
                _tiledSyncPath = null;
                _watchedPngPaths.Clear();
                _watchedTsxPaths.Clear();
            }
        }

        public void RecordOwnSave(string filePath)
        {
            _coalescer.RecordOwnSave(Canonicalize(filePath),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        // FileSystemWatcher.StartRaisingEvents throws on a directory path containing "../"
        // segments, and watcher events report fully-resolved paths — so collapse ".." (and
        // unify slashes) up front. Without this the ctor crashes on textures referenced via
        // "../" and, even when it didn't, stored paths could never match the resolved events.
        private static string Canonicalize(string path)
        {
            try { return new FilePath(path).StandardizedCaseSensitive ?? path.Replace('\\', '/'); }
            catch (InvalidOperationException) { return path.Replace('\\', '/'); }
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
            static string Norm(string p) => p.Replace('\\', '/');

            fsw.Changed += (_, e) =>
            {
                if (!IsEnabled) return;
                _coalescer.Record(Norm(e.FullPath), WatcherChangeType.Modified, Now());
            };
            fsw.Created += (_, e) =>
            {
                if (!IsEnabled) return;
                _coalescer.Record(Norm(e.FullPath), WatcherChangeType.Created, Now());
            };
            fsw.Deleted += (_, e) =>
            {
                if (!IsEnabled) return;
                _coalescer.Record(Norm(e.FullPath), WatcherChangeType.Deleted, Now());
            };
            fsw.Renamed += (_, e) =>
            {
                if (!IsEnabled) return;
                // Renamed is another form of atomic-write on some tools
                _coalescer.Record(Norm(e.OldFullPath), WatcherChangeType.Deleted, Now());
                _coalescer.Record(Norm(e.FullPath), WatcherChangeType.Created, Now());
            };

            _watchers[directory] = fsw;
        }

        private void FlushCoalescer()
        {
            if (!IsEnabled) return;

            string? achxPath;
            string? tiledSyncPath;
            HashSet<string> pngPaths;
            HashSet<string> tsxPaths;

            lock (_lock)
            {
                achxPath = _achxPath;
                tiledSyncPath = _tiledSyncPath;
                pngPaths = new HashSet<string>(_watchedPngPaths, StringComparer.OrdinalIgnoreCase);
                tsxPaths = new HashSet<string>(_watchedTsxPaths, StringComparer.OrdinalIgnoreCase);
            }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var events = _coalescer.Flush(nowMs);

            foreach (var (path, type) in events)
            {
                var normalizedPath = path.Replace('\\', '/');
                bool isAchx = achxPath != null &&
                    string.Equals(normalizedPath, achxPath, StringComparison.OrdinalIgnoreCase);
                bool isPng = pngPaths.Contains(normalizedPath);
                bool isTiledSync = tiledSyncPath != null &&
                    string.Equals(normalizedPath, tiledSyncPath, StringComparison.OrdinalIgnoreCase);
                bool isTsx = tsxPaths.Contains(normalizedPath);

                if (!isAchx && !isPng && !isTiledSync && !isTsx) continue;

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
                else if (isTiledSync)
                {
                    if (type != WatcherChangeType.Deleted)
                        TiledSyncChangedOnDisk?.Invoke(normalizedPath);
                }
                else if (isTsx)
                {
                    if (type != WatcherChangeType.Deleted)
                        AssociatedTsxChangedOnDisk?.Invoke(normalizedPath);
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
