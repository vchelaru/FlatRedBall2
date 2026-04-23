using System.Linq;
using AsepriteDotNet;
using AsepriteDotNet.Aseprite;
using AsepriteDotNet.Processors;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AseAnimationFrame = AsepriteDotNet.AnimationFrame;
using AseTexture = AsepriteDotNet.Texture;

namespace FlatRedBall2.Content.Aseprite;

/// <summary>
/// Extension methods for loading and processing Aseprite files into FlatRedBall2 animation and texture types.
/// </summary>
public static class AsepriteFileExtensions
{
    /// <summary>
    /// Name used for the fallback chain when an Aseprite file has no tags defined.
    /// </summary>
    public const string UntaggedChainName = "Default";

    /// <summary>
    /// Converts an Aseprite file to a runtime <see cref="AnimationChainList"/>.
    /// Creates a <see cref="Texture2D"/> from the processed spritesheet in GPU memory.
    /// Each Aseprite tag becomes one <see cref="AnimationChain"/>.
    /// If the file has no tags, a single chain named <see cref="UntaggedChainName"/>
    /// is created containing every frame in order.
    /// </summary>
    public static AnimationChainList ToAnimationChainList(this AsepriteFile file, GraphicsDevice graphicsDevice)
    {
        SpriteSheet spriteSheet = SpriteSheetProcessor.Process(file, onlyVisibleLayers: true,
            includeBackgroundLayer: false, includeTilemapLayers: false,
            mergeDuplicateFrames: true, borderPadding: 0, spacing: 0, innerPadding: 0);
        AseTexture aseTexture = spriteSheet.TextureAtlas.Texture;

        var texture = new Texture2D(graphicsDevice, aseTexture.Size.Width, aseTexture.Size.Height);
        texture.SetData(aseTexture.Pixels.ToArray());

        var regions = spriteSheet.TextureAtlas.Regions.ToArray();
        var list = new AnimationChainList();

        var tags = spriteSheet.Tags.ToArray();
        if (tags.Length == 0)
        {
            list.Add(BuildUntaggedChain(file, regions, texture));
            return list;
        }

        foreach (AnimationTag tag in tags)
        {
            var chain = new AnimationChain { Name = tag.Name };

            foreach (AseAnimationFrame aseFrame in tag.Frames)
            {
                var bounds = regions[aseFrame.FrameIndex].Bounds;
                var frame = new FlatRedBall2.Animation.AnimationFrame
                {
                    Texture = texture,
                    FrameLength = aseFrame.Duration,
                    SourceRectangle = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                };
                chain.Add(frame);
            }

            list.Add(chain);
        }

        return list;
    }

    /// <summary>
    /// Converts an Aseprite file to an <see cref="AnimationChainListSave"/> with pixel coordinates.
    /// Does not require a graphics device — use this for offline .achx generation.
    /// The caller is responsible for saving the spritesheet texture separately.
    /// If the file has no tags, a single chain named <see cref="UntaggedChainName"/>
    /// is created containing every frame in order.
    /// </summary>
    /// <param name="file">The loaded Aseprite file.</param>
    /// <param name="textureFileName">
    /// Texture file name to embed in each frame (e.g. "Player.png").
    /// Should be relative to the .achx output location.
    /// </param>
    public static AnimationChainListSave ToAnimationChainListSave(this AsepriteFile file, string textureFileName)
    {
        SpriteSheet spriteSheet = SpriteSheetProcessor.Process(file, onlyVisibleLayers: true,
            includeBackgroundLayer: false, includeTilemapLayers: false,
            mergeDuplicateFrames: true, borderPadding: 0, spacing: 0, innerPadding: 0);
        var regions = spriteSheet.TextureAtlas.Regions.ToArray();

        var save = new AnimationChainListSave
        {
            CoordinateType = TextureCoordinateType.Pixel,
            TimeMeasurementUnit = TimeMeasurementUnit.Second,
        };

        var tags = spriteSheet.Tags.ToArray();
        if (tags.Length == 0)
        {
            save.AnimationChains.Add(BuildUntaggedChainSave(file, regions, textureFileName));
            return save;
        }

        foreach (AnimationTag tag in tags)
        {
            var chainSave = new AnimationChainSave { Name = tag.Name };

            foreach (AseAnimationFrame aseFrame in tag.Frames)
            {
                var bounds = regions[aseFrame.FrameIndex].Bounds;
                chainSave.Frames.Add(new AnimationFrameSave
                {
                    TextureName = textureFileName,
                    FrameLength = (float)aseFrame.Duration.TotalSeconds,
                    LeftCoordinate = bounds.X,
                    TopCoordinate = bounds.Y,
                    RightCoordinate = bounds.X + bounds.Width,
                    BottomCoordinate = bounds.Y + bounds.Height,
                });
            }

            save.AnimationChains.Add(chainSave);
        }

        return save;
    }

    // Fallback: single chain covering every frame at its authored duration.
    private static AnimationChain BuildUntaggedChain(AsepriteFile file, TextureRegion[] regions, Texture2D texture)
    {
        var chain = new AnimationChain { Name = UntaggedChainName };
        for (int i = 0; i < file.Frames.Length && i < regions.Length; i++)
        {
            var bounds = regions[i].Bounds;
            chain.Add(new FlatRedBall2.Animation.AnimationFrame
            {
                Texture = texture,
                FrameLength = file.Frames[i].Duration,
                SourceRectangle = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            });
        }
        return chain;
    }

    private static AnimationChainSave BuildUntaggedChainSave(AsepriteFile file, TextureRegion[] regions, string textureFileName)
    {
        var chainSave = new AnimationChainSave { Name = UntaggedChainName };
        for (int i = 0; i < file.Frames.Length && i < regions.Length; i++)
        {
            var bounds = regions[i].Bounds;
            chainSave.Frames.Add(new AnimationFrameSave
            {
                TextureName = textureFileName,
                FrameLength = (float)file.Frames[i].Duration.TotalSeconds,
                LeftCoordinate = bounds.X,
                TopCoordinate = bounds.Y,
                RightCoordinate = bounds.X + bounds.Width,
                BottomCoordinate = bounds.Y + bounds.Height,
            });
        }
        return chainSave;
    }
}
