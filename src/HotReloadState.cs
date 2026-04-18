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

    public void Set<T>(string key, T value) => _values[key] = value;

    public T Get<T>(string key) => (T)_values[key]!;

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
}
