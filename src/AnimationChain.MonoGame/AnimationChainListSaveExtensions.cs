using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Converts a portable <see cref="AnimationChainListSave"/> (the .achx/.achj data model, shared
/// with the Animation Editor and the main FlatRedBall2 engine via <c>AnimationEditorCommon</c>)
/// into this package's own <see cref="AnimationChain{TFrame}"/>/<see cref="AnimationChainList{TFrame}"/>
/// runtime closed over <see cref="AnimationFrame"/>. Lives in this package (not
/// <c>AnimationEditorCommon</c> itself) because it needs <see cref="Texture2D"/> and this package's
/// own "sticky" per-frame color semantics (see <see cref="ToAnimationChainList"/>).
/// </summary>
public static class AnimationChainListSaveExtensions
{
    /// <summary>
    /// Converts this save to a runtime <see cref="AnimationChainList{TFrame}"/>. Texture paths are
    /// resolved relative to the .achx file location (via <see cref="AnimationChainListSave.FileName"/>,
    /// when <see cref="AnimationChainListSave.FileRelativeTextures"/> is <c>true</c>) and passed to
    /// <paramref name="textureLoader"/>.
    /// <para>
    /// For most callers, prefer <see cref="AchxLoader.Load(string)"/>, which wraps this call and
    /// adds texture caching so the same spritesheet is not uploaded more than once.
    /// </para>
    /// </summary>
    /// <param name="save">The parsed .achx/.achj data to convert.</param>
    /// <param name="textureLoader">
    /// Called with the resolved texture path. May return <c>null</c> if the texture is
    /// unavailable — the frame will have a <c>null</c> texture.
    /// </param>
    public static AnimationChainList<AnimationFrame> ToAnimationChainList(
        this AnimationChainListSave save, Func<string, Texture2D?> textureLoader)
    {
        string achxDir = string.IsNullOrEmpty(save.FileName) ? "" : Path.GetDirectoryName(save.FileName) ?? "";

        return BuildList(save, frameSave =>
        {
            if (string.IsNullOrEmpty(frameSave.TextureName)) return null;
            string texPath = save.FileRelativeTextures && !string.IsNullOrEmpty(achxDir)
                ? Path.Combine(achxDir, frameSave.TextureName)
                : frameSave.TextureName;
            return textureLoader(texPath);
        });
    }

    // Shared chain-building logic; loadTexture maps a save frame to its Texture2D (or null).
    private static AnimationChainList<AnimationFrame> BuildList(AnimationChainListSave save, Func<AnimationFrameSave, Texture2D?> loadTexture)
    {
        float frameLengthDivisor = save.TimeMeasurementUnit == TimeMeasurementUnit.Millisecond ? 1000f : 1f;
        var list = new AnimationChainList<AnimationFrame> { Name = save.FileName };

        foreach (var chainSave in save.AnimationChains)
        {
            var chain = new AnimationChain<AnimationFrame> { Name = chainSave.Name, Loop = chainSave.Loop };

            // Sticky color resolution: a frame that omits a channel inherits the most recent
            // explicitly-set value from an earlier frame in this chain, rather than resetting to
            // null. Mirrors the Animation Editor's EffectiveFrameColor.ResolveAll. This is specific
            // to this package's runtime -- AnimationEditorCommon's own conversion (used by the
            // main engine's Sprite animation) does not apply it.
            int? stickyRed = null, stickyGreen = null, stickyBlue = null, stickyAlpha = null;
            FlatRedBall2.Animation.ColorOperation? stickyOperation = null;

            foreach (var frameSave in chainSave.Frames)
            {
                stickyRed       = frameSave.Red           ?? stickyRed;
                stickyGreen     = frameSave.Green         ?? stickyGreen;
                stickyBlue      = frameSave.Blue          ?? stickyBlue;
                stickyAlpha     = frameSave.Alpha         ?? stickyAlpha;
                stickyOperation = frameSave.ColorOperation ?? stickyOperation;

                var frame = new AnimationFrame
                {
                    TextureName = frameSave.TextureName,
                    FrameLength = TimeSpan.FromSeconds(frameSave.FrameLength / frameLengthDivisor),
                    FlipHorizontal = frameSave.FlipHorizontal,
                    FlipVertical = frameSave.FlipVertical,
                    FlipDiagonal = frameSave.FlipDiagonal,
                    RelativeX = frameSave.RelativeX,
                    RelativeY = frameSave.RelativeY,
                    Red = stickyRed,
                    Green = stickyGreen,
                    Blue = stickyBlue,
                    Alpha = stickyAlpha,
                    ColorOperation = stickyOperation,
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
                        frame.SourceRectangle = new PixelRectangle(left, top, width, height);
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

    /// <summary>
    /// Re-parses the .achx/.achj at <paramref name="achxPath"/> and applies changes to
    /// <paramref name="list"/> in place so any live <see cref="AnimationPlayer{TFrame}"/> reference
    /// keeps working. For each chain in the reloaded file, matches by
    /// <see cref="AnimationChain{TFrame}.Name"/>: existing chains have their frames replaced in
    /// place (instance identity preserved); new chains are appended.
    /// <para>
    /// Returns <c>false</c> on I/O or parse failure (e.g. file mid-write) — XML or JSON, depending
    /// on <paramref name="achxPath"/>'s extension. Callers should retry after the next
    /// file-system debounce window.
    /// </para>
    /// <para>
    /// Named <c>TryReload</c>, not <c>TryReloadFrom</c>, on purpose: <see cref="AnimationChainList{TFrame}"/>
    /// already declares its own instance <c>TryReloadFrom</c> overloads (frame-factory shaped). An
    /// extension method with the same name is silently shadowed by the (textually applicable,
    /// wrong-shaped) instance overload whenever the argument list also happens to type-check against
    /// it — <c>list.TryReloadFrom(path, _ => null)</c> would compile fine but call the frame-factory
    /// overload with a factory that always returns <c>null</c>, dropping every frame. A distinct name
    /// sidesteps the collision.
    /// </para>
    /// </summary>
    /// <param name="list">The list to update in place.</param>
    /// <param name="achxPath">Path to the .achx/.achj file to reload from.</param>
    /// <param name="textureLoader">
    /// Called with the resolved absolute path of each texture file. May return <c>null</c>
    /// if the texture is unavailable — affected frames keep a <c>null</c> texture.
    /// Typically supplied by <see cref="AchxLoader"/>, which reuses its internal cache
    /// and only loads new textures.
    /// </param>
    public static bool TryReload(this AnimationChainList<AnimationFrame> list, string achxPath, Func<string, Texture2D?> textureLoader)
    {
        AnimationChainList<AnimationFrame> fresh;
        try
        {
            var save = achxPath.EndsWith(".achj", StringComparison.OrdinalIgnoreCase)
                ? AnimationChainListSave.FromJsonFile(achxPath)
                : AnimationChainListSave.FromFile(achxPath);
            // AnimationEditorCommon's FromFile/FromJsonFile store FileName verbatim (not resolved
            // to an absolute path) -- resolve it here so ToAnimationChainList's achxDir-based
            // texture-path resolution below produces an absolute path.
            save.FileName = Path.GetFullPath(achxPath);
            fresh = save.ToAnimationChainList(textureLoader);
        }
        catch (IOException) { return false; }
        catch (System.Xml.XmlException) { return false; }
        catch (System.Text.Json.JsonException) { return false; }

        ApplyReloadedChains(list, fresh);
        return true;
    }

    /// <summary>
    /// Re-parses .achx (XML) or .achj (JSON) from an already-open <paramref name="achxStream"/> --
    /// dialect auto-detected from the content, since no file path is available to inspect an
    /// extension (see <see cref="AnimationChainListSave.FromStream"/>) -- and applies
    /// changes to <paramref name="list"/> in place, same merge semantics as
    /// <see cref="TryReload(AnimationChainList{AnimationFrame}, string, Func{string, Texture2D?})"/>.
    /// </summary>
    /// <param name="list">The list to update in place.</param>
    /// <param name="achxStream">Readable stream containing .achx or .achj data. Caller retains ownership.</param>
    /// <param name="textureLoader">
    /// Called with each texture path stored in the .achx data. May return <c>null</c> if
    /// a texture is unavailable — affected frames keep a <c>null</c> texture.
    /// </param>
    public static bool TryReload(this AnimationChainList<AnimationFrame> list, Stream achxStream, Func<string, Texture2D?> textureLoader)
    {
        AnimationChainList<AnimationFrame> fresh;
        try
        {
            var save = AnimationChainListSave.FromStream(achxStream);
            fresh = save.ToAnimationChainList(textureLoader);
        }
        catch (IOException) { return false; }
        catch (System.Xml.XmlException) { return false; }
        catch (System.Text.Json.JsonException) { return false; }

        ApplyReloadedChains(list, fresh);
        return true;
    }

    private static void ApplyReloadedChains(AnimationChainList<AnimationFrame> list, AnimationChainList<AnimationFrame> fresh)
    {
        foreach (var freshChain in fresh)
        {
            var existing = list[freshChain.Name];
            if (existing != null)
            {
                existing.Clear();
                existing.AddRange(freshChain);
            }
            else
            {
                list.Add(freshChain);
            }
        }
    }
}
