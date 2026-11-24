using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Export;

/// <summary>The kind of a <see cref="ExportShape"/>.</summary>
public enum ExportShapeKind { Rect, Circle, Polygon }

/// <summary>
/// A collision shape as the preview shows it at an instant: offset and size already glided by the shape's curves.
/// For a rectangle <paramref name="Param1"/>/<paramref name="Param2"/> are the half-width and half-height, for a circle
/// <paramref name="Param1"/> is the radius; a polygon's <paramref name="Points"/> are x,y pairs relative to X/Y.
/// </summary>
public sealed record ExportShape(
    string Name, ExportShapeKind Kind, float X, float Y, float Param1, float Param2, float[]? Points = null, bool IsClosed = true);

/// <summary>Samples the shapes of one frame the way the preview does.</summary>
public static class ExportShapes
{
    public static IReadOnlyList<ExportShape> Sample(AnimationChainSave chain, FrameLengthUnit unit, int frameIndex, double elapsedInFrame)
    {
        var shapes = chain.Frames[frameIndex].ShapesSave;
        if (shapes is null) return [];

        var list = new List<ExportShape>();
        foreach (var r in shapes.AARectSaves)
        {
            var v = KeyframeSampler.SampleShape(chain, unit, frameIndex, elapsedInFrame, r);
            list.Add(new ExportShape(r.Name, ExportShapeKind.Rect, v.X, v.Y, v.SizeX, v.SizeY));
        }
        foreach (var c in shapes.CircleSaves)
        {
            var v = KeyframeSampler.SampleShape(chain, unit, frameIndex, elapsedInFrame, c);
            list.Add(new ExportShape(c.Name, ExportShapeKind.Circle, v.X, v.Y, v.SizeX, 0f));
        }
        foreach (var p in shapes.PolygonSaves)
        {
            var sampled = KeyframeSampler.SampleShapePoints(chain, unit, frameIndex, elapsedInFrame, p);
            int n = PolygonVertices.Count(sampled);
            var points = new float[n * 2];
            for (int i = 0; i < n; i++)
                (points[i * 2], points[i * 2 + 1]) = (sampled[i].X, sampled[i].Y);
            var v = KeyframeSampler.SampleShape(chain, unit, frameIndex, elapsedInFrame, p);
            list.Add(new ExportShape(p.Name, ExportShapeKind.Polygon, v.X, v.Y, 0f, 0f, points, sampled.Count >= 2 && sampled[0] == sampled[^1]));
        }
        return list;
    }
}
