using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// A scrollbar's range and thumb derived from a pan axis: the four values an Avalonia
/// <c>ScrollBar</c> needs (<see cref="Minimum"/>, <see cref="Maximum"/>, <see cref="Value"/>,
/// <see cref="ViewportSize"/>).
/// </summary>
public readonly record struct ScrollBarRange(
    float Minimum, float Maximum, float Value, float ViewportSize);

/// <summary>
/// Pure mapping between a center-relative pan axis and a scrollbar, used to drive the
/// Preview panel's scrollbars from its existing manual pan (#415) instead of hosting the
/// content in a <c>ScrollViewer</c>.
/// <para>
/// The scroll axis runs <b>opposite</b> to pan — scrolling toward the high end moves the
/// content the other way, so the content origin's pan <i>decreases</i>. The mapping is
/// therefore a negation: <c>value = -pan</c>. The value range is the pan band from
/// <see cref="CanvasTransform.PanRange"/>, so it grows with zoom (which grows the on-screen
/// content extent), and <see cref="ScrollBarRange.ViewportSize"/> is the viewport extent so
/// the thumb reflects how much of the content is visible.
/// </para>
/// </summary>
public static class PanScrollBar
{
    /// <summary>
    /// Maps a center-relative <paramref name="pan"/> to a scrollbar range/thumb.
    /// <paramref name="viewExtent"/> is the viewport size on this axis;
    /// <paramref name="contentMin"/>/<paramref name="contentMax"/> are the content's on-screen
    /// extent relative to the origin. The pan is clamped into the band so the thumb stays on
    /// the track.
    /// </summary>
    public static ScrollBarRange FromPan(
        float pan, float viewExtent, float contentMin, float contentMax, float padding = 0f)
    {
        var (min, max) = CanvasTransform.PanRange(viewExtent, contentMin, contentMax, padding);
        float value = Math.Clamp(pan, min, max);
        // Negate to invert direction; [min, max] → [-max, -min] stays ordered since max >= min.
        return new ScrollBarRange(-max, -min, -value, viewExtent);
    }

    /// <summary>Inverse of <see cref="FromPan"/>: the pan a scrollbar value represents.</summary>
    public static float PanFromValue(float scrollValue) => -scrollValue;
}
