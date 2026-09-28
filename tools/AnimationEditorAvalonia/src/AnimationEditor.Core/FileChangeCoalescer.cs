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

        /// <summary>
        /// Optional content check for an event on a path this editor has saved: return true when
        /// the file on disk still holds what this editor wrote (the event is our own echo, drop
        /// it), false when something else has written it since (fire it). When set, it replaces
        /// the <see cref="CooldownMs"/> timing heuristic, which both swallows an external write
        /// landing inside the window (Tiled saving the same tsx a beat after this editor) and
        /// fires on our own write's echo when a loaded machine delivers it late. A null result
        /// means the check couldn't read the file; the event stays pending and is re-checked on
        /// the next flush. A null delegate keeps the pure timing rule.
        /// </summary>
        public Func<string, bool?>? IsStillOwnContent { get; set; }

        public void Record(string path, WatcherChangeType type, long timestampMs)
        {
            path = path.Replace('\\', '/');
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
            path = path.Replace('\\', '/');
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

                    // Discard events that were triggered by our own save, and remove them from
                    // pending so they never fire. With a content check, the file still holding
                    // what we wrote decides it however late the FSW event arrived; without one,
                    // an event within CooldownMs of our save is assumed to be caused by it.
                    if (_ownSaves.TryGetValue(kv.Key, out long saveTs))
                    {
                        bool? isOwn = IsStillOwnContent is null
                            ? kv.Value.Ts - saveTs < CooldownMs
                            : IsStillOwnContent(kv.Key);
                        if (isOwn is null) continue; // can't tell yet; retry next flush
                        if (isOwn == true)
                        {
                            ready.Add(kv.Key);
                            continue;
                        }
                    }

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
