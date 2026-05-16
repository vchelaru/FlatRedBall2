using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.HotReload
{
    /// <summary>
    /// Pure, clock-injectable file-change coalescer. Handles debounce, own-save cooldown,
    /// and the atomic-write pattern (Delete + Create → Modified). No FileSystemWatcher,
    /// no timers — drive it by calling Record/RecordOwnSave then Flush.
    /// </summary>
    public sealed class FileChangeCoalescer
    {
        public long DebounceMs { get; set; } = 200;
        public long CooldownMs { get; set; } = 500;
        public long AtomicWriteMs { get; set; } = 100;

        private readonly object _lock = new();

        // pending[path] = (latestTimestamp, changeType)
        private readonly Dictionary<string, (long Ts, WatcherChangeType Type)> _pending =
            new(StringComparer.OrdinalIgnoreCase);

        // pending deletes waiting to see if a Create arrives (atomic-write detection)
        private readonly Dictionary<string, long> _pendingDeletes =
            new(StringComparer.OrdinalIgnoreCase);

        // ownSaves[path] = timestamp of own save (for cooldown gate)
        private readonly Dictionary<string, long> _ownSaves =
            new(StringComparer.OrdinalIgnoreCase);

        public void Record(string path, WatcherChangeType type, long timestampMs)
        {
            lock (_lock)
            {
                if (type == WatcherChangeType.Deleted)
                {
                    // Track as pending delete — wait for possible Create (atomic-write)
                    _pendingDeletes[path] = timestampMs;
                    return;
                }

                if (type == WatcherChangeType.Created)
                {
                    // Did we see a Delete recently? → atomic-write → treat as Modified
                    if (_pendingDeletes.TryGetValue(path, out long delTs) &&
                        timestampMs - delTs <= AtomicWriteMs)
                    {
                        _pendingDeletes.Remove(path);
                        type = WatcherChangeType.Modified;
                    }
                    else
                    {
                        _pendingDeletes.Remove(path);
                    }
                }

                // Upsert: reset timestamp on each new event for same path (debounce reset)
                _pending[path] = (timestampMs, type);
            }
        }

        public void RecordOwnSave(string path, long timestampMs)
        {
            lock (_lock)
            {
                _ownSaves[path] = timestampMs;
            }
        }

        /// <summary>
        /// Returns coalesced events whose debounce window has elapsed and which are not
        /// suppressed by the own-save cooldown. Removes returned events from pending.
        /// Also promotes any pending deletes whose atomic-write window has elapsed.
        /// </summary>
        public IReadOnlyList<(string Path, WatcherChangeType Type)> Flush(long nowMs)
        {
            lock (_lock)
            {
                // Promote stale pending deletes (Create never arrived)
                var expiredDeletes = new List<string>();
                foreach (var kv in _pendingDeletes)
                {
                    if (nowMs - kv.Value > AtomicWriteMs)
                    {
                        expiredDeletes.Add(kv.Key);
                        _pending[kv.Key] = (kv.Value, WatcherChangeType.Deleted);
                    }
                }
                foreach (var p in expiredDeletes)
                    _pendingDeletes.Remove(p);

                // Collect ready events
                var result = new List<(string, WatcherChangeType)>();
                var ready = new List<string>();
                foreach (var kv in _pending)
                {
                    if (nowMs - kv.Value.Ts < DebounceMs) continue; // still in debounce window

                    // Check own-save cooldown
                    if (_ownSaves.TryGetValue(kv.Key, out long saveTs) &&
                        nowMs - saveTs < CooldownMs)
                        continue;

                    ready.Add(kv.Key);
                    result.Add((kv.Key, kv.Value.Type));
                }

                foreach (var p in ready)
                    _pending.Remove(p);

                return result;
            }
        }
    }
}
