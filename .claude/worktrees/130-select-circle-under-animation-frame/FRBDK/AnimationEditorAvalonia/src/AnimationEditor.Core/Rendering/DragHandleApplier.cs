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

        // Resize: enforce minimum 1-pixel size; frame may extend outside bitmap.
        float l  = Math.Min(nb.Left,   nb.Right  - 1f);
        float t  = Math.Min(nb.Top,    nb.Bottom - 1f);
        float r  = Math.Max(nb.Right,  nb.Left   + 1f);
        float bm = Math.Max(nb.Bottom, nb.Top    + 1f);

        return new(l, t, r, bm);
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
    /// </remarks>
    public static BoundsRect SnapEdges(BoundsRect bounds, HandleKind handle, int snapSize)
    {
        if (snapSize <= 0) return bounds;

        float Snap(float v) => MathF.Round(v / snapSize) * snapSize;

        float l = bounds.Left, t = bounds.Top, r = bounds.Right, b = bounds.Bottom;
        return handle switch
        {
            HandleKind.Move =>
                // Preserve size; snap top-left corner only.
                new BoundsRect(Snap(l), Snap(t), Snap(l) + (r - l), Snap(t) + (b - t)),
            HandleKind.TopLeft   => new BoundsRect(Snap(l), Snap(t), r, b),
            HandleKind.TopCenter => new BoundsRect(l, Snap(t), r, b),
            HandleKind.TopRight  => new BoundsRect(l, Snap(t), Snap(r), b),
            HandleKind.MidLeft   => new BoundsRect(Snap(l), t, r, b),
            HandleKind.MidRight  => new BoundsRect(l, t, Snap(r), b),
            HandleKind.BotLeft   => new BoundsRect(Snap(l), t, r, Snap(b)),
            HandleKind.BotCenter => new BoundsRect(l, t, r, Snap(b)),
            HandleKind.BotRight  => new BoundsRect(l, t, Snap(r), Snap(b)),
            _                    => bounds,
        };
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
