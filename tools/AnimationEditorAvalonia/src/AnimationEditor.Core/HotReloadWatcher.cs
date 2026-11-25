using AnimationEditor.Core.Paths;
using System;
using System.Collections.Concurrent;
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

        // Held across an own save's write + record, and across the flush's content check, so the
        // timer thread never hashes a half-written file (or a written-but-unrecorded one) and
        // mistakes the editor's own save for an external change (#1223).
        private readonly object _ownSaveLock = new();

        // Directory path → watcher
        private readonly Dictionary<string, FileSystemWatcher> _watchers =
            new(StringComparer.OrdinalIgnoreCase);

        // Directory path → watcher of the folders in it: the existing folders above each one that holds
        // a watched file, up to the first that holds the main file too. A watcher stays with the folder
        // it was opened on, so only the folder above reports that one created, deleted or replaced.
        private readonly Dictionary<string, FileSystemWatcher> _parentWatchers =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentQueue<string> _changedFolders = new();

        private string? _achxPath;
        private readonly HashSet<string> _watchedPngPaths =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _watchedSourcePaths =
            new(StringComparer.OrdinalIgnoreCase);

        public event Action<string>? AchxChangedOnDisk;
        public event Action<string>? PngChangedOnDisk;
        public event Action<string>? AchxDeletedOnDisk;
        public event Action<string>? SourceChangedOnDisk;

        public bool IsEnabled { get; set; } = true;

        /// <summary>How long a changed file may stay unreadable before its change is reported anyway.</summary>
        internal long UnreadableLimitMs { get; set; } = 10_000;

        // When each changed file was first found unreadable. Touched under _lock.
        private readonly Dictionary<string, long> _unreadableSince =
            new(StringComparer.OrdinalIgnoreCase);

        // ownSaveHashes[canonical path] = SHA-256 of the file as this editor last wrote it, or null
        // when it was unreadable right after the write (filled in on the first readable check).
        private readonly Dictionary<string, byte[]?> _ownSaveHashes =
            new(StringComparer.OrdinalIgnoreCase);

        // seenHashes[canonical path] = SHA-256 of the watched file as the editor last loaded, saved
        // or reported it. An event on a file still holding that content is dropped: copying a
        // texture (an APFS clone on macOS) or touching its timestamp raises one on an unchanged file.
        private readonly Dictionary<string, byte[]> _seenHashes =
            new(StringComparer.OrdinalIgnoreCase);

        public HotReloadWatcher()
        {
            _coalescer = new FileChangeCoalescer { IsStillOwnContent = IsStillOwnContent };
            _flushTimer = new Timer(_ => FlushCoalescer(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void StartWatching(string achxPath, IEnumerable<string> pngPaths)
        {
            StopWatching();

            lock (_lock)
            {
                _achxPath = Canonicalize(achxPath);
                _watchedPngPaths.Clear();
                foreach (var p in pngPaths)
                    _watchedPngPaths.Add(Canonicalize(p));

                SyncWatchersLocked();

                _seenHashes.Clear();
                RememberContent(_achxPath);
                foreach (var png in _watchedPngPaths)
                    RememberContent(png);
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

                foreach (var p in added)   { _watchedPngPaths.Add(p); RememberContent(p); }
                foreach (var p in removed) { _watchedPngPaths.Remove(p); _seenHashes.Remove(p); }

                SyncWatchersLocked();
            }
        }

        public void UpdateSourceList(IEnumerable<string> sourcePaths)
        {
            lock (_lock)
            {
                var newSet = new HashSet<string>(
                    sourcePaths.Select(Canonicalize),
                    StringComparer.OrdinalIgnoreCase);
                var (added, removed) = ReferencedFileDiff.Diff(_watchedSourcePaths, newSet);

                foreach (var p in added)   { _watchedSourcePaths.Add(p); RememberContent(p); }
                foreach (var p in removed) { _watchedSourcePaths.Remove(p); _seenHashes.Remove(p); }

                SyncWatchersLocked();
            }
        }

        // Caller holds _lock. Watches exactly the folders of the main file, the PNGs and the sources,
        // and the folders above each: adds the missing ones and drops the ones nothing needs any more.
        private void SyncWatchersLocked()
        {
            var needed = NeededFoldersLocked();
            while (true)
            {
                foreach (var dir in needed.Folders) AddWatcher(_watchers, dir, CreateFileWatcher);
                foreach (var dir in needed.Parents) AddWatcher(_parentWatchers, dir, CreateFolderWatcher);

                // A folder created before the one above it was watched raised no event.
                var now = NeededFoldersLocked();
                if (now.Folders.SetEquals(needed.Folders) && now.Parents.SetEquals(needed.Parents)) break;
                needed = now;
            }

            Drop(_watchers, dir => !needed.Folders.Contains(dir));
            Drop(_parentWatchers, dir => !needed.Parents.Contains(dir));
        }

        // Caller holds _lock.
        private IEnumerable<string> WatchedFilesLocked() =>
            _watchedPngPaths.Concat(_watchedSourcePaths).Concat(string.IsNullOrEmpty(_achxPath) ? [] : [_achxPath]);

        // Caller holds _lock.
        private (HashSet<string> Folders, HashSet<string> Parents) NeededFoldersLocked()
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in WatchedFilesLocked().Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                if (Directory.Exists(dir)) folders.Add(dir);
                for (var parent = Path.GetDirectoryName(dir); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
                {
                    if (!Directory.Exists(parent)) continue;
                    parents.Add(parent);
                    if (_achxPath?.StartsWith(Norm(parent).TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase) == true) break;
                }
            }
            return (folders, parents);
        }

        private static void Drop(Dictionary<string, FileSystemWatcher> watchers, Func<string, bool> drop)
        {
            foreach (var dir in watchers.Keys.Where(drop).ToList())
            {
                watchers[dir].Dispose();
                watchers.Remove(dir);
            }
        }

        /// <summary>Watches the folders that were created, deleted or replaced since the last flush as they are now,
        /// and reports their files, whose own events the new watchers were too late for.</summary>
        private void FollowChangedFolders()
        {
            var changed = new List<string>();
            while (_changedFolders.TryDequeue(out var folder)) changed.Add(folder + "/");
            if (changed.Count == 0) return;

            bool IsIn(string path) => changed.Any(folder => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
            bool IsChanged(string dir) => IsIn(Norm(dir) + "/");
            var reports = new List<(string Path, WatcherChangeType Type)>();
            lock (_lock)
            {
                var files = WatchedFilesLocked().Where(IsIn).ToList();
                if (files.Count == 0) return;

                Drop(_watchers, IsChanged);
                Drop(_parentWatchers, IsChanged);
                SyncWatchersLocked();

                foreach (var file in files)
                {
                    if (File.Exists(file)) reports.Add((file, WatcherChangeType.Created));
                    else if (_seenHashes.ContainsKey(file)) reports.Add((file, WatcherChangeType.Deleted));
                }
            }
            if (!IsEnabled) return;
            // Outside _lock: the coalescer takes its own lock first.
            foreach (var (file, type) in reports)
                _coalescer.Record(file, type, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public void StopWatching()
        {
            _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);

            lock (_lock)
            {
                Drop(_watchers, _ => true);
                Drop(_parentWatchers, _ => true);
                _achxPath = null;
                _watchedPngPaths.Clear();
                _watchedSourcePaths.Clear();
                _unreadableSince.Clear();
            }
        }

        public void RunOwnSave(string filePath, Action write)
        {
            var canonical = Canonicalize(filePath);
            lock (_ownSaveLock)
            {
                write();
                // Null when something (antivirus, an indexer) holds the file right after our
                // write; IsStillOwnContent then takes the first readable content as ours.
                var hash = TryHash(canonical);
                lock (_lock)
                {
                    _ownSaveHashes[canonical] = hash;
                    if (hash is null) _seenHashes.Remove(canonical);
                    else _seenHashes[canonical] = hash;
                }
                _coalescer.RecordOwnSave(canonical, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }

        /// <inheritdoc/>
        public void ReportChange(string filePath)
        {
            var canonical = Canonicalize(filePath);
            lock (_ownSaveLock)
            {
                lock (_lock)
                {
                    _ownSaveHashes.Remove(canonical);
                    _seenHashes.Remove(canonical);
                }
                _coalescer.Record(canonical, WatcherChangeType.Modified, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }

        /// <summary>Whether <paramref name="filePath"/> still holds exactly what the last <see
        /// cref="RunOwnSave"/> wrote: false for a file never saved here or since deleted, null when
        /// the file exists but can't be read right now (another process holds it), so the caller
        /// retries instead of treating a lock as an external write.</summary>
        internal bool? IsStillOwnContent(string filePath)
        {
            var canonical = Canonicalize(filePath);
            lock (_ownSaveLock)
            {
                byte[]? recorded;
                lock (_lock)
                    if (!_ownSaveHashes.TryGetValue(canonical, out recorded)) return false;
                var current = TryHash(canonical);
                if (current is null) return File.Exists(canonical) ? null : false;
                if (recorded is null)
                {
                    lock (_lock)
                        _ownSaveHashes[canonical] = current;
                    return true;
                }
                return current.AsSpan().SequenceEqual(recorded);
            }
        }

        // Caller holds _lock.
        private void RememberContent(string canonicalPath)
        {
            var hash = TryHash(canonicalPath);
            if (hash is null) _seenHashes.Remove(canonicalPath);
            else _seenHashes[canonicalPath] = hash;
        }

        /// <summary>False when <paramref name="path"/> still holds the content last seen, so the
        /// event is noise; otherwise records the new content and returns true. Null when the file
        /// exists but can't be read right now (its writer still holds it): the release raises no
        /// event of its own, so the caller keeps this one until the file can be read. True once that has
        /// lasted <see cref="UnreadableLimitMs"/>, so a file that stays unreadable is not retried forever.</summary>
        private bool? ContentChangedSinceSeen(string path, WatcherChangeType type, long nowMs)
        {
            lock (_lock)
            {
                if (type == WatcherChangeType.Deleted)
                {
                    _seenHashes.Remove(path);
                    _unreadableSince.Remove(path);
                    return true;
                }
                var current = TryHash(path);
                if (current is null && File.Exists(path))
                {
                    if (!_unreadableSince.TryGetValue(path, out long since)) _unreadableSince[path] = since = nowMs;
                    if (nowMs - since < UnreadableLimitMs) return null;
                }
                _unreadableSince.Remove(path);
                if (current != null && _seenHashes.TryGetValue(path, out var seen) &&
                    current.AsSpan().SequenceEqual(seen))
                    return false;
                if (current is null) _seenHashes.Remove(path);
                else _seenHashes[path] = current;
                return true;
            }
        }

        internal static byte[]? TryHash(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                return System.Security.Cryptography.SHA256.HashData(stream);
            }
            catch
            {
                return null;
            }
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

        private static void AddWatcher(
            Dictionary<string, FileSystemWatcher> watchers, string directory, Func<string, FileSystemWatcher> create)
        {
            if (watchers.ContainsKey(directory)) return;

            FileSystemWatcher? fsw = null;
            try
            {
                fsw = create(directory);
                fsw.EnableRaisingEvents = true;
                watchers[directory] = fsw;
            }
            // Deleted since it was found to exist, or a folder the user may not list.
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                fsw?.Dispose();
            }
        }

        private static string Norm(string path) => path.Replace('\\', '/');

        private FileSystemWatcher CreateFolderWatcher(string directory)
        {
            var fsw = new FileSystemWatcher(directory) { NotifyFilter = NotifyFilters.DirectoryName };
            fsw.Created += (_, e) => _changedFolders.Enqueue(Norm(e.FullPath));
            fsw.Deleted += (_, e) => _changedFolders.Enqueue(Norm(e.FullPath));
            fsw.Renamed += (_, e) =>
            {
                _changedFolders.Enqueue(Norm(e.OldFullPath));
                _changedFolders.Enqueue(Norm(e.FullPath));
            };
            // Raised when this folder itself is gone, on the platforms that say so.
            fsw.Error += (_, _) => _changedFolders.Enqueue(Norm(directory));
            return fsw;
        }

        private FileSystemWatcher CreateFileWatcher(string directory)
        {
            var fsw = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = false,
            };

            long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

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

            return fsw;
        }

        private void FlushCoalescer()
        {
            FollowChangedFolders();
            if (!IsEnabled) return;

            string? achxPath;
            HashSet<string> pngPaths;
            HashSet<string> sourcePaths;

            lock (_lock)
            {
                achxPath = _achxPath;
                pngPaths = new HashSet<string>(_watchedPngPaths, StringComparer.OrdinalIgnoreCase);
                sourcePaths = new HashSet<string>(_watchedSourcePaths, StringComparer.OrdinalIgnoreCase);
            }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // _ownSaveLock before the coalescer's own lock, matching RunOwnSave's order: Flush
            // calls IsStillOwnContent, which takes _ownSaveLock, from inside the coalescer lock.
            IReadOnlyList<(string Path, WatcherChangeType Type)> events;
            lock (_ownSaveLock)
                events = _coalescer.Flush(nowMs);

            foreach (var (path, type) in events)
            {
                var normalizedPath = path.Replace('\\', '/');
                bool isAchx = achxPath != null &&
                    string.Equals(normalizedPath, achxPath, StringComparison.OrdinalIgnoreCase);
                bool isSource = !isAchx && sourcePaths.Contains(normalizedPath);
                bool isPng = !isAchx && !isSource && pngPaths.Contains(normalizedPath);

                if (!isAchx && !isSource && !isPng) continue;
                var changed = ContentChangedSinceSeen(normalizedPath, type, nowMs);
                if (changed is null) _coalescer.Record(normalizedPath, type, nowMs);
                if (changed != true) continue;

                if (isAchx)
                {
                    if (type == WatcherChangeType.Deleted)
                        AchxDeletedOnDisk?.Invoke(normalizedPath);
                    else
                        AchxChangedOnDisk?.Invoke(normalizedPath);
                }
                else if (isSource)
                {
                    // Deleted too: the consumer re-reads the file and finds it gone.
                    SourceChangedOnDisk?.Invoke(normalizedPath);
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
            StopWatching();
            _flushTimer.Dispose();
        }
    }
}
