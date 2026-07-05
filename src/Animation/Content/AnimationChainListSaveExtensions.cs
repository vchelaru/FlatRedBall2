using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Animation.Content;

/// <summary>
/// Converts the portable <see cref="AnimationChainListSave"/>/<see cref="ShapesSave"/> data model
/// into runtime engine types. Lives in the main engine assembly (not alongside
/// <see cref="AnimationChainListSave"/> itself) because it needs <see cref="ContentLoader"/> and
/// MonoGame's <see cref="Texture2D"/> — the Save types stay in a MonoGame-free assembly so tooling
/// (the Animation Editor) can reference the .achx data model without pulling in MonoGame at all.
/// </summary>
public static class AnimationChainListSaveExtensions
{
    /// <summary>
    /// Converts this save object to a runtime <see cref="AnimationChainList"/>, loading
    /// all referenced textures through <see cref="ContentLoader.Load{T}"/>.
    /// Texture paths are passed as-is: if the frame's <c>TextureName</c> includes an
    /// extension (e.g. <c>"Player.png"</c>), it loads directly from disk and participates
    /// in PNG hot-reload via <see cref="ContentLoader.TryReload"/>; if there is
    /// no extension, it goes through MonoGame's compiled xnb pipeline (not hot-reloadable).
    /// </summary>
    /// <remarks>
    /// Texture names are resolved relative to the .achx file location when
    /// <see cref="AnimationChainListSave.FileRelativeTextures"/> is <c>true</c>.
    /// </remarks>
    public static AnimationChainList ToAnimationChainList(this AnimationChainListSave save, ContentLoader contentManager)
    {
        string achxDir = string.IsNullOrEmpty(save.FileName) ? "" : Path.GetDirectoryName(save.FileName) ?? "";

        return BuildList(save, frameSave =>
        {
            if (string.IsNullOrEmpty(frameSave.TextureName)) return null;

            string texPath = save.FileRelativeTextures && !string.IsNullOrEmpty(achxDir)
                ? Path.Combine(achxDir, frameSave.TextureName)
                : frameSave.TextureName;

            return contentManager.Load<Texture2D>(texPath.Replace('\\', '/'));
        });
    }

    // Shared chain-building logic; loadTexture maps a save frame to its Texture2D (or null).
    private static AnimationChainList BuildList(AnimationChainListSave save, Func<AnimationFrameSave, Texture2D?> loadTexture)
    {
        float frameLengthDivisor = save.TimeMeasurementUnit == TimeMeasurementUnit.Millisecond ? 1000f : 1f;
        var list = new AnimationChainList { Name = save.FileName };

        foreach (var chainSave in save.AnimationChains)
        {
            var chain = new AnimationChain { Name = chainSave.Name };

            foreach (var frameSave in chainSave.Frames)
            {
                var frame = new AnimationFrame
                {
                    TextureName = frameSave.TextureName,
                    FrameLength = TimeSpan.FromSeconds(frameSave.FrameLength / frameLengthDivisor),
                    FlipHorizontal = frameSave.FlipHorizontal,
                    FlipVertical = frameSave.FlipVertical,
                    FlipDiagonal = frameSave.FlipDiagonal,
                    RelativeX = frameSave.RelativeX,
                    RelativeY = frameSave.RelativeY,
                    Red = frameSave.Red,
                    Green = frameSave.Green,
                    Blue = frameSave.Blue,
                    Alpha = frameSave.Alpha,
                    ColorOperation = frameSave.ColorOperation,
                };

                frame.Texture = loadTexture(frameSave);

                if (frame.Texture != null)
                {
                    int left, top, width, height;
                    if (save.CoordinateType == TextureCoordinateType.Pixel)
                    {
                        left   = (int)frameSave.LeftCoordinate;
                        top    = (int)frameSave.TopCoordinate;
                        width  = (int)(frameSave.RightCoordinate  - frameSave.LeftCoordinate);
                        height = (int)(frameSave.BottomCoordinate - frameSave.TopCoordinate);
                    }
                    else // UV
                    {
                        left   = (int)(frameSave.LeftCoordinate   * frame.Texture.Width);
                        top    = (int)(frameSave.TopCoordinate    * frame.Texture.Height);
                        width  = (int)((frameSave.RightCoordinate  - frameSave.LeftCoordinate) * frame.Texture.Width);
                        height = (int)((frameSave.BottomCoordinate - frameSave.TopCoordinate)  * frame.Texture.Height);
                    }

                    if (width > 0 && height > 0)
                        frame.SourceRectangle = new Rectangle(left, top, width, height);
                }

                AppendShapes(frame, frameSave.ShapesSave);

                chain.Add(frame);
            }

            list.Add(chain);
        }

        return list;
    }

    private static void AppendShapes(AnimationFrame frame, ShapesSave? shapes)
    {
        if (shapes == null) return;

        foreach (var shape in shapes.Shapes)
        {
            switch (shape)
            {
                case AARectSave rect:
                    ValidateName(rect.Name, "AARectSave");
                    frame.Shapes.Add(new AnimationAARectFrame
                    {
                        Name = rect.Name,
                        RelativeX = rect.X,
                        RelativeY = rect.Y,
                        Width = rect.ScaleX * 2f,
                        Height = rect.ScaleY * 2f,
                    });
                    break;
                case CircleSave circle:
                    ValidateName(circle.Name, "CircleSave");
                    frame.Shapes.Add(new AnimationCircleFrame
                    {
                        Name = circle.Name,
                        RelativeX = circle.X,
                        RelativeY = circle.Y,
                        Radius = circle.Radius,
                    });
                    break;
                case PolygonSave poly:
                    ValidateName(poly.Name, "PolygonSave");
                    var points = new System.Numerics.Vector2[poly.Points.Count];
                    for (int i = 0; i < poly.Points.Count; i++)
                        points[i] = new System.Numerics.Vector2(poly.Points[i].X, poly.Points[i].Y);
                    frame.Shapes.Add(new AnimationPolygonFrame
                    {
                        Name = poly.Name,
                        RelativeX = poly.X,
                        RelativeY = poly.Y,
                        Points = points,
                    });
                    break;
            }
        }
    }

    private static void ValidateName(string name, string elementType)
    {
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException(
                $"{elementType} in .achx ShapesSave is missing a Name. Per-frame shapes must have non-empty unique names.");
    }
}
