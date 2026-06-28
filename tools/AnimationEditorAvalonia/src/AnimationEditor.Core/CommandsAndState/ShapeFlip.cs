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
/// path in <c>AppCommands.DuplicateChain</c> so both use identical logic.
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
}
