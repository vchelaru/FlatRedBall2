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
    /// flip toggling, given the frame's <em>current</em> <paramref name="flipHorizontal"/>/
    /// <paramref name="flipVertical"/> state (unaffected by the diagonal toggle itself).
    /// </summary>
    /// <remarks>
    /// Shape offsets are stored already-baked for the current flip state (there is no separate
    /// canonical/unflipped copy), so toggling diagonal must apply the <em>delta</em> between the
    /// old and new baked state, not a fixed transpose. That delta is a plain swap (x, y) -> (y, x)
    /// when <paramref name="flipHorizontal"/> and <paramref name="flipVertical"/> agree (both set
    /// or both clear), but a negated swap (x, y) -> (-y, -x) when exactly one of them is set —
    /// baking diagonal after horizontal (or vice versa) with the wrong sign here is what put a
    /// shape in the mirror-image spot instead of the correct one (issue #592 follow-up). Toggling
    /// diagonal a second time re-applies the same delta (same H/V inputs), which is self-inverse
    /// regardless of sign, so undo/redo still restores the original values exactly. A rectangle's
    /// <c>ScaleX</c>/<c>ScaleY</c> (half-width/half-height) swap unconditionally — no sign
    /// ambiguity for magnitudes. A circle's radius is orientation-independent and untouched.
    /// </remarks>
    public static void Transpose(object shape, bool flipHorizontal, bool flipVertical)
    {
        bool negate = flipHorizontal ^ flipVertical;
        switch (shape)
        {
            case AARectSave r:
                (r.X, r.Y) = negate ? (-r.Y, -r.X) : (r.Y, r.X);
                (r.ScaleX, r.ScaleY) = (r.ScaleY, r.ScaleX);
                break;
            case CircleSave c:
                (c.X, c.Y) = negate ? (-c.Y, -c.X) : (c.Y, c.X);
                break;
            case PolygonSave p:
                (p.X, p.Y) = negate ? (-p.Y, -p.X) : (p.Y, p.X);
                foreach (var pt in p.Points)
                    (pt.X, pt.Y) = negate ? (-pt.Y, -pt.X) : (pt.Y, pt.X);
                break;
        }
    }
}
