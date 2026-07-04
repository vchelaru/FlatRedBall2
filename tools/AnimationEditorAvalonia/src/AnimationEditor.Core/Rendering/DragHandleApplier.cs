using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Axis-aligned bounding rectangle expressed as four edge coordinates.
/// Used by <see cref="DragHandleApplier"/> to avoid a SkiaSharp dependency in Core.
/// </summary>
public readonly record struct BoundsRect(float Left, float Top, float Right, float Bottom);

/// <summary>
/// Pure, SkiaSharp-free drag-handle math for frame UV rectangles.
/// Computes new texture-pixel bounds after a handle drag and converts them to UV coords.
/// </summary>
public static class DragHandleApplier
{
    /// <summary>
    /// Returns the new texture-pixel bounds after dragging <paramref name="handle"/>
    /// by (<paramref name="dx"/>, <paramref name="dy"/>) from <paramref name="startBounds"/>.
    /// The frame may extend freely outside the bitmap boundaries; only a minimum
    /// dimension of 1 pixel on each axis is enforced (resize handles only).
    /// </summary>
    public static BoundsRect Apply(
        HandleKind handle,
        float dx, float dy,
        BoundsRect startBounds)
    {
        var b = startBounds;

        BoundsRect nb = handle switch
        {
            HandleKind.Move      => new(b.Left + dx, b.Top + dy, b.Right  + dx, b.Bottom + dy),
            HandleKind.TopLeft   => new(b.Left + dx, b.Top + dy, b.Right,        b.Bottom),
            HandleKind.TopCenter => new(b.Left,       b.Top + dy, b.Right,        b.Bottom),
            HandleKind.TopRight  => new(b.Left,       b.Top + dy, b.Right  + dx,  b.Bottom),
            HandleKind.MidLeft   => new(b.Left + dx,  b.Top,       b.Right,        b.Bottom),
            HandleKind.MidRight  => new(b.Left,        b.Top,       b.Right  + dx,  b.Bottom),
            HandleKind.BotLeft   => new(b.Left + dx,  b.Top,       b.Right,        b.Bottom + dy),
            HandleKind.BotCenter => new(b.Left,        b.Top,       b.Right,        b.Bottom + dy),
            HandleKind.BotRight  => new(b.Left,        b.Top,       b.Right  + dx,  b.Bottom + dy),
            _                    => b,
        };

        // Move slides the whole frame as a rigid body — dimensions already preserved.
        if (handle == HandleKind.Move)
            return nb;

        // Resize: enforce a 1-pixel minimum by clamping the dragged edge against the
        // fixed opposite edge; frame may extend outside bitmap.
        return ClampDraggedEdgesMinSize(nb, handle);
    }

    /// <summary>Which of the four edges a resize handle moves (false for all with Move/None).</summary>
    private static (bool Left, bool Right, bool Top, bool Bottom) DraggedEdges(HandleKind handle) => (
        handle is HandleKind.TopLeft  or HandleKind.MidLeft   or HandleKind.BotLeft,
        handle is HandleKind.TopRight or HandleKind.MidRight  or HandleKind.BotRight,
        handle is HandleKind.TopLeft  or HandleKind.TopCenter or HandleKind.TopRight,
        handle is HandleKind.BotLeft  or HandleKind.BotCenter or HandleKind.BotRight);

    /// <summary>
    /// Clamps each dragged edge so it stays at least 1 pixel from the fixed opposite
    /// edge, keeping the fixed edge anchored. Dragging (or snapping) an edge past the
    /// far side collapses the frame to 1 pixel instead of moving the fixed edge — which
    /// would slide the frame sideways and/or invert it (handles rendering on the inside).
    /// </summary>
    private static BoundsRect ClampDraggedEdgesMinSize(BoundsRect b, HandleKind handle)
    {
        var (dl, dr, dt, db) = DraggedEdges(handle);
        float l = b.Left, t = b.Top, r = b.Right, bm = b.Bottom;
        if (dl) l  = MathF.Min(l,  r  - 1f);
        if (dr) r  = MathF.Max(r,  l  + 1f);
        if (dt) t  = MathF.Min(t,  bm - 1f);
        if (db) bm = MathF.Max(bm, t  + 1f);
        return new BoundsRect(l, t, r, bm);
    }

    /// <summary>
    /// Snaps only the edges that <paramref name="handle"/> controls to the nearest
    /// multiple of <paramref name="snapSize"/>, leaving unaffected edges unchanged.
    /// Pass <paramref name="snapSize"/> = 1 to snap to integer pixels.
    /// When <paramref name="snapSize"/> is ≤ 0 the bounds are returned unchanged.
    /// </summary>
    /// <remarks>
    /// For <see cref="HandleKind.Move"/> the top-left corner is snapped and the
    /// original width/height is preserved so the frame does not drift in size.
    /// For resize handles a snapped edge is clamped so it never crosses the fixed
    /// opposite edge (minimum 1 pixel, matching <see cref="Apply"/>): otherwise,
    /// when the fixed edge is off-grid, snapping the dragged edge could land it past
    /// the fixed edge and invert the frame.
    /// </remarks>
    public static BoundsRect SnapEdges(BoundsRect bounds, HandleKind handle, int snapSize)
    {
        if (snapSize <= 0) return bounds;

        float Snap(float v) => MathF.Round(v / snapSize) * snapSize;

        float l = bounds.Left, t = bounds.Top, r = bounds.Right, b = bounds.Bottom;

        if (handle == HandleKind.Move)
            // Preserve size; snap top-left corner only.
            return new BoundsRect(Snap(l), Snap(t), Snap(l) + (r - l), Snap(t) + (b - t));

        var (dl, dr, dt, db) = DraggedEdges(handle);
        if (dl) l = Snap(l);
        if (dr) r = Snap(r);
        if (dt) t = Snap(t);
        if (db) b = Snap(b);

        // A snap can land the dragged edge past the (possibly off-grid) fixed edge —
        // clamp it so the frame collapses to 1px instead of inverting.
        return ClampDraggedEdgesMinSize(new BoundsRect(l, t, r, b), handle);
    }

    /// <summary>
    /// Converts texture-pixel bounds to UV coordinates (0…1 relative to bitmap dimensions).
    /// Returns (left, top, right, bottom) UV tuple.
    /// </summary>
    public static (float L, float T, float R, float B) ToUvCoords(
        BoundsRect bounds, float bitmapWidth, float bitmapHeight) =>
        (bounds.Left   / bitmapWidth,
         bounds.Top    / bitmapHeight,
         bounds.Right  / bitmapWidth,
         bounds.Bottom / bitmapHeight);
}
