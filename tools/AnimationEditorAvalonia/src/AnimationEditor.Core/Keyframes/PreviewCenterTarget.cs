using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Keyframes;

/// <summary>Where the preview should center for a double-clicked tree row: the point the row's object draws at (#1925).</summary>
public static class PreviewCenterTarget
{
    /// <summary>
    /// Entity-space point of a frame row (its sampled offset) or a shape row (its sampled offset; a polygon's
    /// bounding-box center, since its X/Y is only the origin its points hang from), at the start of frame
    /// <paramref name="frameIndex"/>. Returns null for any other row.
    /// </summary>
    public static (float X, float Y)? For(AnimationChainSave chain, FrameLengthUnit unit, int frameIndex, object row)
    {
        switch (row)
        {
            case AnimationFrameSave:
            {
                var s = KeyframeSampler.Sample(chain, unit, frameIndex, 0);
                return (s.OffsetX, s.OffsetY);
            }
            case PolygonSave polygon:
            {
                var v = KeyframeSampler.SampleShape(chain, unit, frameIndex, 0, polygon);
                var points = KeyframeSampler.SampleShapePoints(chain, unit, frameIndex, 0, polygon);
                if (points.Count == 0) return (v.X, v.Y);
                float minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
                float minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
                return (v.X + (minX + maxX) / 2f, v.Y + (minY + maxY) / 2f);
            }
            case ShapeSave shape:
            {
                var v = KeyframeSampler.SampleShape(chain, unit, frameIndex, 0, shape);
                return (v.X, v.Y);
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// <see cref="For"/> for a row not in any chain (nothing to sample): the stored values, which is
    /// what every keyframe-free row draws at anyway.
    /// </summary>
    public static (float X, float Y)? ForUnattached(object row) => row switch
    {
        AnimationFrameSave frame => (frame.RelativeX, frame.RelativeY),
        PolygonSave polygon when polygon.Points.Count > 0 => (
            polygon.X + (polygon.Points.Min(p => p.X) + polygon.Points.Max(p => p.X)) / 2f,
            polygon.Y + (polygon.Points.Min(p => p.Y) + polygon.Points.Max(p => p.Y)) / 2f),
        ShapeSave shape => (shape.X, shape.Y),
        _ => null,
    };
}
