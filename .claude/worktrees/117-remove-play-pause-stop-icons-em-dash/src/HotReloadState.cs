using System;
using System.Collections.Generic;

namespace FlatRedBall2;

/// <summary>
/// Typed key/value bag passed to <see cref="Screen.SaveHotReloadState"/> and
/// <see cref="Screen.RestoreHotReloadState"/>. Use it to carry game state across a hot-reload
/// restart so the player isn't sent back to spawn when a content file changes on disk.
/// <para>
/// Only used by <see cref="Screen.RestartScreen(RestartMode)"/> with <see cref="RestartMode.HotReload"/>.
/// Plain death/retry restarts never invoke the hooks and never construct this bag.
/// </para>
/// </summary>
public class HotReloadState
{
    private readonly Dictionary<string, object?> _values = new();
    private readonly Dictionary<Type, int> _saveCounts = new();
    private readonly Dictionary<Type, int> _restoreCounts = new();

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, overwriting any previous value.</summary>
    public void Set<T>(string key, T value) => _values[key] = value;

    /// <summary>
    /// Retrieves the value previously stored under <paramref name="key"/>, cast to <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">No entry exists for <paramref name="key"/>.</exception>
    /// <exception cref="System.InvalidCastException">The stored value is not assignable to <typeparamref name="T"/>.</exception>
    public T Get<T>(string key) => (T)_values[key]!;

    /// <summary>
    /// Attempts to retrieve the value stored under <paramref name="key"/> as <typeparamref name="T"/>.
    /// Returns <c>false</c> if the key is absent or the stored value is the wrong type — pair with
    /// "leave default" semantics on the first hot-reload before any prior save.
    /// </summary>
    public bool TryGet<T>(string key, out T value)
    {
        if (_values.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Returns the next auto-generated key for <paramref name="type"/> during the save phase,
    /// formatted as <c>"{Type.Name}_{n}"</c> with a per-type counter that starts at 1. Used by
    /// the auto-keyed <c>Preserve</c> overload in <see cref="HotReloadStateExtensions"/>.
    /// </summary>
    internal string NextSaveKey(Type type)
    {
        _saveCounts.TryGetValue(type, out var n);
        _saveCounts[type] = ++n;
        return $"{type.Name}_{n}";
    }

    /// <summary>
    /// Returns the next auto-generated key for <paramref name="type"/> during the restore phase.
    /// Kept independent from the save-phase counter because save and restore run on different
    /// screen instances and may even share the same <see cref="HotReloadState"/> instance.
    /// </summary>
    internal string NextRestoreKey(Type type)
    {
        _restoreCounts.TryGetValue(type, out var n);
        _restoreCounts[type] = ++n;
        return $"{type.Name}_{n}";
    }
}
