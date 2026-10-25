using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;

namespace FlatRedBall2;

/// <summary>
/// KNI <see cref="ContentManager"/> that can unload a single asset. KNI keeps one flat disposable
/// list per manager and has no <c>UnloadAsset</c>, so this owns its own cache: each load records
/// the disposables it creates into that asset's own set via <c>ReadAsset</c>'s callback.
/// External references inside an asset load through <see cref="Load{T}"/> and get their own
/// entry, so unloading a parent never disposes a child it shares with another asset.
/// </summary>
internal class UnloadableContentManager : ContentManager
{
    private sealed class Entry
    {
        public object? Asset;
        public readonly HashSet<IDisposable> Disposables = new();
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public UnloadableContentManager(IServiceProvider serviceProvider, string rootDirectory)
        : base(serviceProvider, rootDirectory)
    {
    }

    public override T Load<T>(string assetName)
    {
        if (string.IsNullOrEmpty(assetName))
            throw new ArgumentNullException(nameof(assetName));

        var key = NormalizeKey(assetName);
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var cached) && cached.Asset is T hit)
                return hit;
        }

        // No lock across ReadAsset: KNI supports loading from several threads, and external
        // references re-enter Load. Each load records into its own set, so callbacks from
        // concurrent loads can't land in the wrong asset.
        var disposables = new HashSet<IDisposable>();
        T result = ReadAsset<T>(assetName, d => { lock (disposables) disposables.Add(d); });

        lock (_lock)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry();
                _entries[key] = entry;
            }
            else if (entry.Asset is T raced)
            {
                // Another thread finished the same load first; keep its instance.
                foreach (var d in disposables)
                    d.Dispose();
                return raced;
            }
            // A cached asset of a different T is replaced (as KNI's own Load does), but its
            // disposables stay on the entry so UnloadAsset still releases them.
            entry.Asset = result;
            entry.Disposables.UnionWith(disposables);
        }
        return result;
    }

    /// <summary>
    /// Disposes everything the named asset's load created and drops it from the cache, so the
    /// next <see cref="Load{T}"/> reads it again. Does nothing if the asset isn't loaded.
    /// </summary>
    public void UnloadAsset(string assetName)
    {
        Entry? entry;
        lock (_lock)
        {
            if (!_entries.Remove(NormalizeKey(assetName), out entry))
                return;
        }
        foreach (var d in entry.Disposables)
            d.Dispose();
    }

    public override void Unload()
    {
        List<Entry> entries;
        lock (_lock)
        {
            entries = new List<Entry>(_entries.Values);
            _entries.Clear();
        }
        foreach (var entry in entries)
            foreach (var d in entry.Disposables)
                d.Dispose();
        base.Unload();
    }

    // Same key KNI's own Load uses: forward slashes, compared case-insensitively.
    private static string NormalizeKey(string assetName) => assetName.Replace('\\', '/');
}
