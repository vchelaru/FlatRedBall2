using System;

namespace AnimationEditor.Core.Sequences;

/// <summary>What sits at a height on the sequence timeline.</summary>
public enum TimelineRowKind
{
    /// <summary>Above the lanes: the time ruler.</summary>
    Ruler,

    /// <summary>A track's lane; <see cref="TimelineRow.Track"/> says which.</summary>
    Track,

    /// <summary>The dashed row under the last lane, where a dropped chip makes a new track.</summary>
    NewTrack,

    /// <summary>Below the new-track row.</summary>
    None,
}

/// <summary>The row under a point: its kind, and the track index for <see cref="TimelineRowKind.Track"/> (-1 otherwise).</summary>
public readonly record struct TimelineRow(TimelineRowKind Kind, int Track = -1);

/// <summary>
/// Maps sequence time and track rows to timeline pixels, with zoom and scroll. The track headers sit in the first
/// <see cref="HeaderWidth"/> pixels and the ruler in the top <see cref="RulerHeight"/>; both stay put while the lanes
/// scroll under them.
/// </summary>
public sealed class TimelineViewport
{
    public const double HeaderWidth = 120;
    public const double RulerHeight = 22;
    public const double LaneHeight = 36;
    public const double MinPixelsPerSecond = 10;
    public const double MaxPixelsPerSecond = 2000;
    public const double DefaultPixelsPerSecond = 120;

    /// <summary>Zoom: how many pixels one second takes.</summary>
    public double PixelsPerSecond { get; private set; } = DefaultPixelsPerSecond;

    /// <summary>The time at the left edge of the lanes. Never negative.</summary>
    public double ScrollSeconds { get; private set; }

    /// <summary>How far the lanes are scrolled up, in pixels. Never negative.</summary>
    public double ScrollY { get; private set; }

    public double XAt(double time) => HeaderWidth + (time - ScrollSeconds) * PixelsPerSecond;

    public double TimeAt(double x) => ScrollSeconds + (x - HeaderWidth) / PixelsPerSecond;

    /// <summary>The top of row <paramref name="lane"/>, where the track count is the new-track row.</summary>
    public double LaneTop(int lane) => RulerHeight + lane * LaneHeight - ScrollY;

    /// <summary>The height every lane plus the new-track row needs, ruler included.</summary>
    public static double ContentHeight(int trackCount) => RulerHeight + (trackCount + 1) * LaneHeight;

    public TimelineRow RowAt(double y, int trackCount)
    {
        if (y < RulerHeight) return new TimelineRow(TimelineRowKind.Ruler);
        int lane = (int)Math.Floor((y - RulerHeight + ScrollY) / LaneHeight);
        if (lane < trackCount) return new TimelineRow(TimelineRowKind.Track, lane);
        return lane == trackCount ? new TimelineRow(TimelineRowKind.NewTrack) : new TimelineRow(TimelineRowKind.None);
    }

    /// <summary>
    /// Multiplies the zoom by <paramref name="factor"/>, clamped, keeping the time under <paramref name="x"/> where it
    /// is (unless that would scroll before 0).
    /// </summary>
    public void ZoomAround(double x, double factor)
    {
        double anchor = TimeAt(x);
        PixelsPerSecond = Math.Clamp(PixelsPerSecond * factor, MinPixelsPerSecond, MaxPixelsPerSecond);
        ScrollSeconds = Math.Max(0, anchor - (x - HeaderWidth) / PixelsPerSecond);
    }

    /// <summary>Sets the zoom, clamped, keeping the time at the left edge of the lanes where it is.</summary>
    public void SetPixelsPerSecond(double pixelsPerSecond) =>
        PixelsPerSecond = Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);

    /// <summary>The playhead time for a ruler press or drag at <paramref name="x"/>: the time there, clamped to 0..<paramref name="length"/>.</summary>
    public double ScrubTime(double x, double length) => Math.Clamp(TimeAt(x), 0, Math.Max(0, length));

    /// <summary>Scrolls the lanes sideways by <paramref name="pixels"/>; positive shows later time.</summary>
    public void ScrollHorizontally(double pixels) =>
        ScrollSeconds = Math.Max(0, ScrollSeconds + pixels / PixelsPerSecond);

    /// <summary>
    /// Scrolls the lanes up by <paramref name="pixels"/>, kept within the content: nothing to scroll when every lane
    /// fits in <paramref name="viewHeight"/>.
    /// </summary>
    public void ScrollVertically(double pixels, int trackCount, double viewHeight) =>
        ScrollY = Math.Clamp(ScrollY + pixels, 0, Math.Max(0, ContentHeight(trackCount) - viewHeight));

    /// <summary>Re-clamps the vertical scroll after tracks were removed or the view grew.</summary>
    public void ClampVertical(int trackCount, double viewHeight) => ScrollVertically(0, trackCount, viewHeight);
}
