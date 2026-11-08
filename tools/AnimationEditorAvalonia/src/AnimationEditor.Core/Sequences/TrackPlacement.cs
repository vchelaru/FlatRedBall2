using AnimationEditor.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>Which way an arrow key nudges a track; Up is world +Y.</summary>
public enum NudgeDirection { Left, Right, Up, Down }

/// <summary>
/// Where a track's sprite sits on screen: its centre and half size, in the preview's pixels. Built from the sprite as the
/// preview draws it, so a flipped track is boxed where its mirrored frame lands.
/// </summary>
public readonly record struct SpriteBox(int Sequence, int Track, double CenterX, double CenterY, double HalfWidth, double HalfHeight)
{
    public bool Contains(double x, double y) =>
        x >= CenterX - HalfWidth && x <= CenterX + HalfWidth && y >= CenterY - HalfHeight && y <= CenterY + HalfHeight;

    /// <summary>
    /// The box of <paramref name="sprite"/> for a <paramref name="sourceWidth"/> by <paramref name="sourceHeight"/> frame, with the
    /// entity origin at (<paramref name="originX"/>, <paramref name="originY"/>) on screen. Offsets draw scaled by
    /// <paramref name="offsetMultiplier"/> and <paramref name="zoom"/>, the frame's pixels by <paramref name="zoom"/> alone; world Y is up, screen Y down.
    /// </summary>
    public static SpriteBox Of(SequenceSprite sprite, int sourceWidth, int sourceHeight, double zoom, double offsetMultiplier,
        double originX, double originY)
    {
        var sample = sprite.Sample();
        double scale = offsetMultiplier * zoom;
        return new SpriteBox(sprite.Sequence, sprite.Track, originX + sample.OffsetX * scale, originY - sample.OffsetY * scale,
            sourceWidth * zoom / 2, sourceHeight * zoom / 2);
    }
}

/// <summary>
/// The pure math of placing a track from the preview: the drag threshold, screen to world, the whole-pixel snap shared with
/// the <c>.achx</c> preview's frame drag, arrow-key steps, and which sprite is under the pointer.
/// </summary>
public static class TrackPlacement
{
    /// <summary>How far, in pixels on either axis, a press must travel before it is a drag and not a click.</summary>
    public const double DragThresholdPixels = 3;

    /// <summary>Whether a pointer that travelled (<paramref name="screenDx"/>, <paramref name="screenDy"/>) from its press is dragging.</summary>
    public static bool PastDragThreshold(double screenDx, double screenDy) =>
        Math.Abs(screenDx) >= DragThresholdPixels || Math.Abs(screenDy) >= DragThresholdPixels;

    /// <summary>A screen-space delta as an offset delta: divided by zoom and the offset multiplier, with screen Y down turned into world Y up.</summary>
    public static (float Dx, float Dy) ScreenToWorld(double screenDx, double screenDy, double zoom, double offsetMultiplier)
    {
        double scale = zoom * offsetMultiplier;
        return ((float)(screenDx / scale), (float)(-screenDy / scale));
    }

    /// <summary>The nearest whole pixel, halves away from zero: how the <c>.achx</c> preview rounds a dragged offset.</summary>
    public static float SnapToPixel(float value) => MathF.Round(value, MidpointRounding.AwayFromZero) + 0f; // + 0f: -0.3 rounds to -0, which would save as "-0"

    /// <summary>
    /// Each start moved by the world delta and snapped to a whole pixel. Shift (<paramref name="axisLock"/>) keeps only the
    /// larger axis of the delta, as it does for a frame in the <c>.achx</c> preview.
    /// </summary>
    public static IReadOnlyList<(float X, float Y)> Dragged(IReadOnlyList<(float X, float Y)> starts, float dx, float dy, bool axisLock)
    {
        (dx, dy) = AxisLock.Apply(dx, dy, axisLock);
        return starts.Select(start => (SnapToPixel(start.X + dx), SnapToPixel(start.Y + dy))).ToList();
    }

    /// <summary>An arrow key's offset step: 1 unit, 10 with Shift. Up is +Y.</summary>
    public static (float Dx, float Dy) NudgeStep(NudgeDirection direction, bool shift)
    {
        float step = shift ? 10f : 1f;
        return direction switch
        {
            NudgeDirection.Left => (-step, 0f),
            NudgeDirection.Right => (step, 0f),
            NudgeDirection.Up => (0f, step),
            _ => (0f, -step),
        };
    }

    /// <summary>
    /// The sprite under (<paramref name="x"/>, <paramref name="y"/>) that a press grabs: the frontmost box of
    /// <paramref name="sequence"/>, given back to front (the order <see cref="SequenceScene"/> returns). Other sequences'
    /// sprites do not block or take the press. Null when nothing is hit.
    /// </summary>
    public static SpriteBox? TopmostAt(IReadOnlyList<SpriteBox> backToFront, double x, double y, int sequence)
    {
        for (int i = backToFront.Count - 1; i >= 0; i--)
            if (backToFront[i].Sequence == sequence && backToFront[i].Contains(x, y)) return backToFront[i];
        return null;
    }
}
