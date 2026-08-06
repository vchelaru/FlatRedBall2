using AnimationEditor.Core.Export;
using FlatRedBall2.Animation.Content;
using NJsonSchema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests.Export;

public class PixiJsSpriteSheetExporterTests
{
    // All UV-coordinate tests resolve every texture to this fixed size so expected pixel
    // rects are easy to compute (0.5 UV * 64 px = 32 px).
    private static readonly Func<string, (int Width, int Height)?> Size64 = _ => (64, 64);

    private static AnimationFrameSave UvFrame(string textureName, float left, float top, float right, float bottom) =>
        new()
        {
            TextureName = textureName,
            LeftCoordinate = left,
            TopCoordinate = top,
            RightCoordinate = right,
            BottomCoordinate = bottom,
        };

    private static PixiJsSpriteSheet Parse(string json) =>
        JsonSerializer.Deserialize<PixiJsSpriteSheet>(json)!;

    [Fact]
    public void Export_EveryAnimationFrameKey_ExistsInFrames()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 0f, 0f, 0.5f, 0.5f), UvFrame("hero.png", 0.5f, 0f, 1f, 0.5f) },
        });
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Idle",
            Frames = { UvFrame("hero.png", 0f, 0.5f, 0.5f, 1f) },
        });

        var sheet = Parse(PixiJsSpriteSheetExporter.Export(acls, Size64).Text);

        foreach (var key in sheet.Animations.Values.SelectMany(keys => keys))
            Assert.Contains(key, sheet.Frames.Keys);
    }

    [Fact]
    public void Export_MultipleTextures_AddsWarning()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("a.png", 0f, 0f, 1f, 1f), UvFrame("b.png", 0f, 0f, 1f, 1f) },
        });

        var result = PixiJsSpriteSheetExporter.Export(acls, Size64);

        Assert.Contains(result.Warnings, w => w.Contains("references 2 textures"));
    }

    [Fact]
    public async Task Export_Output_ConformsToPixiJsSchema()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 0f, 0f, 0.5f, 0.5f), UvFrame("hero.png", 0.5f, 0f, 1f, 0.5f) },
        });
        var json = PixiJsSpriteSheetExporter.Export(acls, Size64).Text;

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "pixijs-spritesheet.schema.json");
        var schema = await JsonSchema.FromFileAsync(schemaPath);
        var errors = schema.Validate(json);

        Assert.Empty(errors);
    }

    [Fact]
    public void Export_PixelCoordinates_UsedDirectlyWithoutResolver()
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            // Pixel coords: a 24x24 rect at (8, 8).
            Frames = { UvFrame("hero.png", 8f, 8f, 32f, 32f) },
        });

        // Resolver returns null to prove pixel input never consults it.
        var sheet = Parse(PixiJsSpriteSheetExporter.Export(acls, _ => null).Text);

        var rect = sheet.Frames["Walk_0"].Frame;
        Assert.Equal((8, 8, 24, 24), (rect.X, rect.Y, rect.W, rect.H));
    }

    [Fact]
    public void Export_SetsMetaImageFromTextureName()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 0f, 0f, 1f, 1f) },
        });

        var sheet = Parse(PixiJsSpriteSheetExporter.Export(acls, Size64).Text);

        Assert.Equal("hero.png", sheet.Meta.Image);
    }

    [Fact]
    public void Export_UvFrame_ConvertsToExpectedPixelRect()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            // Top-left quadrant of a 64x64 texture -> 32x32 px rect at origin.
            Frames = { UvFrame("hero.png", 0f, 0f, 0.5f, 0.5f) },
        });

        var sheet = Parse(PixiJsSpriteSheetExporter.Export(acls, Size64).Text);

        var rect = sheet.Frames["Walk_0"].Frame;
        Assert.Equal((0, 0, 32, 32), (rect.X, rect.Y, rect.W, rect.H));
    }

    [Fact]
    public void Export_WithMultipleFrames_PreservesAnimationOrderAndFrameKeys()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames =
            {
                UvFrame("hero.png", 0f, 0f, 0.5f, 0.5f),
                UvFrame("hero.png", 0.5f, 0f, 1f, 0.5f),
                UvFrame("hero.png", 0f, 0.5f, 0.5f, 1f),
            },
        });

        var sheet = Parse(PixiJsSpriteSheetExporter.Export(acls, Size64).Text);

        Assert.Equal(new List<string> { "Walk_0", "Walk_1", "Walk_2" }, sheet.Animations["Walk"]);
    }
}
