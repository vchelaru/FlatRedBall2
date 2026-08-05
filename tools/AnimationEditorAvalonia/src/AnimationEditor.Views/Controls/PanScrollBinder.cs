using AnimationEditor.Core.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Two-way binding between an <see cref="IPanScrollTarget"/>'s camera pan and a pair of
/// <c>ScrollBar</c>s (#415 / #422 / #604), the pan-axis counterpart to
/// <see cref="ZoomControl.Attach"/>. One call replaces the per-panel handler pair, refresh method,
/// and feedback-loop suppression flag that each host used to hand-roll.
/// </summary>
public static class PanScrollBinder
{
    /// <summary>
    /// Wires <paramref name="target"/> to <paramref name="horizontal"/> and
    /// <paramref name="vertical"/>: bar edits drive the pan, and the target's
    /// <see cref="IPanScrollTarget.ViewChanged"/> pushes range/thumb back into the bars. The
    /// suppression flag that breaks the echo loop lives here — callers don't manage it.
    /// <para>
    /// <paramref name="onScrollEnd"/> runs when a drag finishes (not per tick), for hosts that
    /// persist the settled pan. Pass <c>null</c> for panels with no state to save.
    /// </para>
    /// </summary>
    public static void Attach(
        IPanScrollTarget target,
        ScrollBar horizontal,
        ScrollBar vertical,
        Action? onScrollEnd = null)
    {
        bool suppress = false;

        horizontal.ValueChanged += (_, _) =>
        {
            if (suppress) return;
            suppress = true;
            target.SetPanX((float)horizontal.Value);
            suppress = false;
        };
        vertical.ValueChanged += (_, _) =>
        {
            if (suppress) return;
            suppress = true;
            target.SetPanY((float)vertical.Value);
            suppress = false;
        };

        target.ViewChanged += () =>
        {
            if (suppress) return;
            suppress = true;
            var (h, v) = target.GetScrollBarRanges();
            ApplyRange(horizontal, h);
            ApplyRange(vertical, v);
            suppress = false;
        };

        if (onScrollEnd is null) return;

        void OnScroll(object? sender, ScrollEventArgs e)
        {
            if (e.ScrollEventType == ScrollEventType.EndScroll) onScrollEnd();
        }

        horizontal.Scroll += OnScroll;
        vertical.Scroll += OnScroll;
    }

    // Order matters: set Minimum/Maximum before Value so RangeBase doesn't coerce it.
    private static void ApplyRange(ScrollBar bar, ScrollBarRange r)
    {
        bar.Minimum      = r.Minimum;
        bar.Maximum      = r.Maximum;
        bar.ViewportSize = r.ViewportSize;
        bar.Value        = r.Value;
    }
}
