using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <summary>The base lane height: a label strip and a filmstrip row, then the cut-tail strip. A track with events is taller (<see cref="TimelineLayout.EventRowHeight"/>); see <see cref="SetLaneHeights"/>.</summary>
    public const double LaneHeight = 48;
    public const double MinPixelsPerSecond = 10;
    public const double MaxPixelsPerSecond = 2000;
    public const double DefaultPixelsPerSecond = 120;

    /// <summary>Raised after any change to the zoom, either scroll offset or the lane heights.</summary>
    public event Action? Changed;

    /// <summary>Sets the time at the left edge of the lanes, never before 0. A scrollbar drives this.</summary>
    public void SetScrollSeconds(double seconds)
    {
        ScrollSeconds = Math.Max(0, seconds);
        Changed?.Invoke();
    }

    /// <summary>Sets the vertical scroll, kept within the content as <see cref="ScrollVertically"/> does.</summary>
    public void SetScrollY(double y, int trackCount, double viewHeight) =>
        ScrollVertically(y - ScrollY, trackCount, viewHeight);

    /// <summary>Zoom: how many pixels one second takes.</summary>
    public double PixelsPerSecond { get; private set; } = DefaultPixelsPerSecond;

    /// <summary>The time at the left edge of the lanes. Never negative.</summary>
    public double ScrollSeconds { get; private set; }

    /// <summary>How far the lanes are scrolled up, in pixels. Never negative.</summary>
    public double ScrollY { get; private set; }

    public double XAt(double time) => HeaderWidth + (time - ScrollSeconds) * PixelsPerSecond;

    public double TimeAt(double x) => ScrollSeconds + (x - HeaderWidth) / PixelsPerSecond;

    /// <summary>The top of row <paramref name="lane"/>, where the track count is the new-track row.</summary>
    public double LaneTop(int lane) => RulerHeight + OffsetOf(lane) - ScrollY;

    // Each track's own lane height (a track with events is taller), and where each lane starts below the ruler. A lane
    // past the list, such as the new-track row, is a base lane.
    private double[] _heights = Array.Empty<double>();
    private double[] _offsets = { 0 };

    /// <summary>
    /// Sets each track's lane height, in track order. Call it whenever the tracks or their contents change, then
    /// <see cref="ClampVertical"/>: the vertical scroll limit depends on it.
    /// </summary>
    public void SetLaneHeights(IReadOnlyList<double> heights)
    {
        _heights = heights.ToArray();
        _offsets = new double[_heights.Length + 1];
        for (int i = 0; i < _heights.Length; i++) _offsets[i + 1] = _offsets[i] + _heights[i];
        Changed?.Invoke();
    }

    /// <summary>The height of lane <paramref name="lane"/>; the new-track row (the track count) is a base <see cref="LaneHeight"/>.</summary>
    public double LaneHeightOf(int lane) => lane >= 0 && lane < _heights.Length ? _heights[lane] : LaneHeight;

    private double OffsetOf(int lane) =>
        lane <= _heights.Length ? _offsets[lane] : _offsets[^1] + (lane - _heights.Length) * LaneHeight;

    /// <summary>The height every lane plus the new-track row needs, ruler included.</summary>
    public double ContentHeight(int trackCount) => RulerHeight + OffsetOf(trackCount) + LaneHeight;

    public TimelineRow RowAt(double y, int trackCount)
    {
        if (y < RulerHeight) return new TimelineRow(TimelineRowKind.Ruler);
        double offset = y - RulerHeight + ScrollY;
        for (int lane = 0; lane < trackCount; lane++)
            if (offset < OffsetOf(lane + 1)) return new TimelineRow(TimelineRowKind.Track, lane);
        return offset < OffsetOf(trackCount) + LaneHeight ? new TimelineRow(TimelineRowKind.NewTrack) : new TimelineRow(TimelineRowKind.None);
    }

    /// <summary>
    /// Multiplies the zoom by <paramref name="factor"/>, clamped, keeping the time under <paramref name="x"/> where it
    /// is (unless that would scroll before 0).
    /// </summary>
    public void ZoomAround(double x, double factor) => SetPixelsPerSecondAround(x, PixelsPerSecond * factor);

    /// <summary>The zoom factor (1.0 = 100%) at <see cref="MinPixelsPerSecond"/>.</summary>
    public const float MinZoom = (float)(MinPixelsPerSecond / DefaultPixelsPerSecond);

    /// <summary>The zoom factor (1.0 = 100%) at <see cref="MaxPixelsPerSecond"/>.</summary>
    public const float MaxZoom = (float)(MaxPixelsPerSecond / DefaultPixelsPerSecond);

    /// <summary>The zoom factor, where 1.0 (100%) is <see cref="DefaultPixelsPerSecond"/>.</summary>
    public float Zoom => (float)(PixelsPerSecond / DefaultPixelsPerSecond);

    /// <summary>
    /// The pixels per second for a zoom factor, with float noise rounded off so a preset lands on its exact value
    /// (100% is exactly <see cref="DefaultPixelsPerSecond"/>) and the zoom limits on exactly the pixel limits.
    /// </summary>
    public static double PixelsPerSecondForZoom(float zoom) =>
        Math.Clamp(Math.Round(zoom * DefaultPixelsPerSecond, 3), MinPixelsPerSecond, MaxPixelsPerSecond);

    /// <summary>
    /// Sets the zoom to <paramref name="pixelsPerSecond"/>, clamped, keeping the time under <paramref name="x"/>
    /// where it is (unless that would scroll before 0).
    /// </summary>
    public void SetPixelsPerSecondAround(double x, double pixelsPerSecond)
    {
        double anchor = TimeAt(x);
        PixelsPerSecond = Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
        ScrollSeconds = Math.Max(0, anchor - (x - HeaderWidth) / PixelsPerSecond);
        Changed?.Invoke();
    }

    /// <summary>Sets the zoom, clamped, keeping the time at the left edge of the lanes where it is.</summary>
    public void SetPixelsPerSecond(double pixelsPerSecond)
    {
        PixelsPerSecond = Math.Clamp(pixelsPerSecond, MinPixelsPerSecond, MaxPixelsPerSecond);
        Changed?.Invoke();
    }

    /// <summary>The playhead time for a ruler press or drag at <paramref name="x"/>: the time there, clamped to 0..<paramref name="length"/>.</summary>
    public double ScrubTime(double x, double length) => Math.Clamp(TimeAt(x), 0, Math.Max(0, length));

    /// <summary>Scrolls the lanes sideways by <paramref name="pixels"/>; positive shows later time.</summary>
    public void ScrollHorizontally(double pixels) => SetScrollSeconds(ScrollSeconds + pixels / PixelsPerSecond);

    /// <summary>
    /// Scrolls the lanes up by <paramref name="pixels"/>, kept within the content: nothing to scroll when every lane
    /// fits in <paramref name="viewHeight"/>.
    /// </summary>
    public void ScrollVertically(double pixels, int trackCount, double viewHeight)
    {
        ScrollY = Math.Clamp(ScrollY + pixels, 0, Math.Max(0, ContentHeight(trackCount) - viewHeight));
        Changed?.Invoke();
    }


    /// <summary>Re-clamps the vertical scroll after tracks were removed or the view grew.</summary>
    public void ClampVertical(int trackCount, double viewHeight) => ScrollVertically(0, trackCount, viewHeight);
}
