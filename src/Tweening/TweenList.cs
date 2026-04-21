using System;
using System.Collections.Generic;
using FlatRedBall.Glue.StateInterpolation;

namespace FlatRedBall2.Tweening;

// Internal helper — owned by Entity and Screen. Updates each tween, drops completed tweens,
// and guarantees the setter sees exactly `to` on completion (the underlying Tweener snaps
// Position *after* its final PositionChanged fires, so the setter wouldn't otherwise land
// on the exact end value).
internal sealed class TweenList
{
    private readonly List<TweenEntry> _entries = new();

    public int Count => _entries.Count;

    public void Add(Tweener tweener, Action<float> setter, float to)
        => _entries.Add(new TweenEntry(tweener, setter, to));

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

    public void Clear() => _entries.Clear();

    private readonly record struct TweenEntry(Tweener Tweener, Action<float> Setter, float To);
}
