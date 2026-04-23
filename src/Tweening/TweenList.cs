using System;
using System.Collections.Generic;
using FlatRedBall.Glue.StateInterpolation;

namespace FlatRedBall2.Tweening;

/// <summary>
/// Internal owner of active <see cref="Tweener"/>s for an <see cref="Entity"/> or <see cref="Screen"/>.
/// Advances each tween, drops it when it stops, and snaps the setter to exactly the requested
/// end value on natural completion — the upstream <see cref="Tweener"/> sets <c>Position</c>
/// after its final <c>PositionChanged</c> fires, so without this guarantee the setter would
/// otherwise land on the second-to-last interpolated value.
/// </summary>
internal sealed class TweenList
{
    private readonly List<TweenEntry> _entries = new();

    /// <summary>Number of currently tracked tweens.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Begins tracking <paramref name="tweener"/>. <paramref name="to"/> is the value the
    /// setter will be invoked with when the tween completes naturally (not when externally
    /// stopped via <see cref="Tweener.Stop"/>).
    /// </summary>
    public void Add(Tweener tweener, Action<float> setter, float to)
        => _entries.Add(new TweenEntry(tweener, setter, to));

    /// <summary>
    /// Advances every tracked tween by <paramref name="dt"/> seconds. Tweens that complete
    /// this frame have their setter invoked with the exact target value, then are removed.
    /// Tweens that were stopped externally before this call are removed without a final
    /// setter invocation.
    /// </summary>
    public void Update(float dt)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            bool wasRunningBefore = entry.Tweener.Running;
            entry.Tweener.Update(dt);
            if (!entry.Tweener.Running)
            {
                // Ran to completion this frame → snap setter to exact `to`.
                // Already stopped before Update (Stop() called externally) → do nothing.
                if (wasRunningBefore)
                    entry.Setter(entry.To);
                _entries.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Drops all tracked tweens without invoking their setters. Called by
    /// <see cref="Entity.Destroy"/> and <see cref="Screen"/> teardown.
    /// </summary>
    public void Clear() => _entries.Clear();

    private readonly record struct TweenEntry(Tweener Tweener, Action<float> Setter, float To);
}
