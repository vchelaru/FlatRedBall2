using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Animation.Content;

/// <summary>
/// Undefined is treated identically to Second and exists for compatibility with .achx files
/// produced by older FlatRedBall tooling.
/// </summary>
public enum TimeMeasurementUnit { Undefined, Second, Millisecond }
public enum TextureCoordinateType { UV, Pixel }

/// <summary>
/// Deserialized representation of a .achx animation file.
/// Load with <see cref="FromFile"/> and convert to runtime types with
/// <see cref="ToAnimationChainList"/>.
/// </summary>
[XmlRoot("AnimationChainArraySave")]
public class AnimationChainListSave
{
    /// <summary>
    /// Whether texture file paths stored in frames are relative to the .achx file location.
    /// Set to <c>true</c> (the default in the .achx format) so the file is portable.
    /// </summary>
    public bool FileRelativeTextures = true;

    public TimeMeasurementUnit TimeMeasurementUnit = TimeMeasurementUnit.Second;
    public TextureCoordinateType CoordinateType = TextureCoordinateType.UV;

    [XmlElement("AnimationChain")]
    public List<AnimationChainSave> AnimationChains = new();

    /// <summary>Absolute path of the .achx file. Set automatically by <see cref="FromFile"/>.</summary>
    [XmlIgnore]
    public string FileName { get; private set; } = string.Empty;

    /// <summary>
    /// Deserializes a .achx file from disk.
    /// </summary>
    /// <param name="filePath">Path to the .achx file, relative to the executable or absolute.</param>
    public static AnimationChainListSave FromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var serializer = new XmlSerializer(typeof(AnimationChainListSave));
        var result = (AnimationChainListSave)serializer.Deserialize(stream)!;
        result.FileName = Path.GetFullPath(filePath);
        return result;
    }

    /// <summary>
    /// Converts this save object to a runtime <see cref="AnimationChainList"/>, loading
    /// all referenced textures through <see cref="ContentManagerService.Load{T}"/>.
    /// Texture paths are passed as-is: if the frame's <c>TextureName</c> includes an
    /// extension (e.g. <c>"Player.png"</c>), it loads directly from disk and participates
    /// in PNG hot-reload via <see cref="ContentManagerService.TryReload"/>; if there is
    /// no extension, it goes through MonoGame's compiled xnb pipeline (not hot-reloadable).
    /// </summary>
    /// <remarks>
    /// Texture names are resolved relative to the .achx file location when
    /// <see cref="FileRelativeTextures"/> is <c>true</c>.
    /// </remarks>
    public AnimationChainList ToAnimationChainList(FlatRedBall2.ContentManagerService contentManager)
    {
        string achxDir = string.IsNullOrEmpty(FileName) ? "" : Path.GetDirectoryName(FileName) ?? "";

        return BuildList(frameSave =>
        {
            if (string.IsNullOrEmpty(frameSave.TextureName)) return null;

            string texPath = FileRelativeTextures && !string.IsNullOrEmpty(achxDir)
                ? Path.Combine(achxDir, frameSave.TextureName)
                : frameSave.TextureName;

            return contentManager.Load<Texture2D>(texPath.Replace('\\', '/'));
        });
    }

    // Shared chain-building logic; loadTexture maps a save frame to its Texture2D (or null).
    private AnimationChainList BuildList(Func<AnimationFrameSave, Texture2D?> loadTexture)
    {
        float frameLengthDivisor = TimeMeasurementUnit == TimeMeasurementUnit.Millisecond ? 1000f : 1f;
        var list = new AnimationChainList { Name = FileName };

        foreach (var chainSave in AnimationChains)
        {
            var chain = new AnimationChain { Name = chainSave.Name };

            foreach (var frameSave in chainSave.Frames)
            {
                var frame = new AnimationFrame
                {
                    TextureName = frameSave.TextureName,
                    FrameLength = frameSave.FrameLength / frameLengthDivisor,
                    FlipHorizontal = frameSave.FlipHorizontal,
                    FlipVertical = frameSave.FlipVertical,
                    RelativeX = frameSave.RelativeX,
                    RelativeY = frameSave.RelativeY,
                };

                frame.Texture = loadTexture(frameSave);

                if (frame.Texture != null)
                {
                    int left, top, width, height;
                    if (CoordinateType == TextureCoordinateType.Pixel)
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

                chain.Add(frame);
            }

            list.Add(chain);
        }

        return list;
    }
}
