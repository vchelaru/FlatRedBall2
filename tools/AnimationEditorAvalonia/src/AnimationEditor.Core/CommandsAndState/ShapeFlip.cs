using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Mirrors a shape's stored offsets to match a frame flip so collision geometry tracks the
/// mirrored sprite. A horizontal flip negates the shape's X, a vertical flip negates its Y,
/// about the entity origin (0,0) — the same space shape offsets live in at runtime
/// (the runtime mirrors only the sprite and applies shape offsets verbatim, so the editor
/// bakes the mirror into the data). Negation is its own exact inverse, so re-applying the
/// same flip restores the original offsets with no rounding drift.
/// <para>
/// Shared by <see cref="Commands.FlipCommand"/> (in-place flip) and the duplicate-with-flip
/// path in <c>AppCommands.DuplicateChains</c> so both use identical logic.
/// </para>
/// </summary>
public static class ShapeFlip
{
    /// <summary>Negates the offsets of <paramref name="shape"/> in place along each flipped axis.</summary>
    public static void Mirror(object shape, bool flipHorizontal, bool flipVertical)
    {
        switch (shape)
        {
            case AARectSave r:
                if (flipHorizontal) r.X = -r.X;
                if (flipVertical)   r.Y = -r.Y;
                break;
            case CircleSave c:
                if (flipHorizontal) c.X = -c.X;
                if (flipVertical)   c.Y = -c.Y;
                break;
            case PolygonSave p:
                // Mirror the origin and every vertex so the polygon outline flips too.
                if (flipHorizontal)
                {
                    p.X = -p.X;
                    foreach (var pt in p.Points) pt.X = -pt.X;
                }
                if (flipVertical)
                {
                    p.Y = -p.Y;
                    foreach (var pt in p.Points) pt.Y = -pt.Y;
                }
                break;
        }
    }

    /// <summary>
    /// Transposes the offsets of <paramref name="shape"/> in place to match a frame's diagonal
    /// flip: (x, y) becomes (-y, -x), the same transpose <c>TileMapCollisions.ApplyFlips</c> uses
    /// for Tiled's diagonal tile-flip flag. A rectangle's <c>ScaleX</c>/<c>ScaleY</c> (half-width/
    /// half-height) swap too, since the transposed region's bounding box swaps width and height.
    /// A circle's radius is orientation-independent and untouched. Self-inverse, like
    /// <see cref="Mirror"/> — applying it twice restores the original values exactly.
    /// </summary>
    public static void Transpose(object shape)
    {
        switch (shape)
        {
            case AARectSave r:
                (r.X, r.Y) = (-r.Y, -r.X);
                (r.ScaleX, r.ScaleY) = (r.ScaleY, r.ScaleX);
                break;
            case CircleSave c:
                (c.X, c.Y) = (-c.Y, -c.X);
                break;
            case PolygonSave p:
                (p.X, p.Y) = (-p.Y, -p.X);
                foreach (var pt in p.Points)
                    (pt.X, pt.Y) = (-pt.Y, -pt.X);
                break;
        }
    }
}
