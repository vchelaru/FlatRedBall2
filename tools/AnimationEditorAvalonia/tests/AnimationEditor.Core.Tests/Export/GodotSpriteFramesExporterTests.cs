using AnimationEditor.Core.Export;
using FlatRedBall2.Animation.Content;
using System;
using Xunit;

namespace AnimationEditor.Core.Tests.Export;

public class GodotSpriteFramesExporterTests
{
    private static readonly Func<string, (int Width, int Height)?> Size64 = _ => (64, 64);

    private static AnimationFrameSave UvFrame(
        string textureName, float left, float top, float right, float bottom,
        float frameLength = 0.1f) =>
        new()
        {
            TextureName = textureName,
            LeftCoordinate = left,
            TopCoordinate = top,
            RightCoordinate = right,
            BottomCoordinate = bottom,
            FrameLength = frameLength,
        };

    [Fact]
    public void Export_EmitsSpriteFramesHeaderAndAnimationName()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 0f, 0f, 0.5f, 0.5f) },
        });

        var text = GodotSpriteFramesExporter.Export(acls, Size64).Text;

        Assert.Contains("[gd_resource type=\"SpriteFrames\" format=3]", text);
        Assert.Contains("&\"Walk\"", text);
        Assert.Contains("\"loop\": true", text);
        Assert.Contains("\"speed\": 1.0", text);
    }

    [Fact]
    public void Export_UvFrame_EmitsAtlasTextureRegion()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 0f, 0f, 0.5f, 0.5f) },
        });

        var text = GodotSpriteFramesExporter.Export(acls, Size64).Text;

        Assert.Contains("region = Rect2(0, 0, 32, 32)", text);
        Assert.Contains("path=\"res://hero.png\"", text);
        Assert.Contains("[sub_resource type=\"AtlasTexture\"", text);
    }

    [Fact]
    public void Export_PixelCoordinates_EmitsRegionWithoutResolver()
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 8f, 8f, 32f, 32f) },
        });

        var text = GodotSpriteFramesExporter.Export(acls, _ => null).Text;

        Assert.Contains("region = Rect2(8, 8, 24, 24)", text);
    }

    [Fact]
    public void Export_FrameLengthSeconds_EmittedAsDuration()
    {
        var acls = new AnimationChainListSave { TimeMeasurementUnit = TimeMeasurementUnit.Second };
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 0f, 0f, 1f, 1f, frameLength: 0.25f) },
        });

        var text = GodotSpriteFramesExporter.Export(acls, Size64).Text;

        Assert.Contains("\"duration\": 0.25", text);
    }

    [Fact]
    public void Export_MillisecondFrameLength_ConvertedToSeconds()
    {
        var acls = new AnimationChainListSave { TimeMeasurementUnit = TimeMeasurementUnit.Millisecond };
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("hero.png", 0f, 0f, 1f, 1f, frameLength: 250f) },
        });

        var text = GodotSpriteFramesExporter.Export(acls, Size64).Text;

        Assert.Contains("\"duration\": 0.25", text);
    }

    [Fact]
    public void Export_MultipleTextures_EmitsMultipleExtResources()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames =
            {
                UvFrame("a.png", 0f, 0f, 1f, 1f),
                UvFrame("b.png", 0f, 0f, 1f, 1f),
            },
        });

        var result = GodotSpriteFramesExporter.Export(acls, Size64);

        Assert.Contains("path=\"res://a.png\"", result.Text);
        Assert.Contains("path=\"res://b.png\"", result.Text);
        Assert.Equal(2, result.ReferencedTextures.Count);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("single sheet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Export_FlipFlags_AddsWarning()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames =
            {
                new AnimationFrameSave
                {
                    TextureName = "hero.png",
                    RightCoordinate = 1f,
                    BottomCoordinate = 1f,
                    FlipHorizontal = true,
                },
            },
        });

        var result = GodotSpriteFramesExporter.Export(acls, Size64);

        Assert.Contains(result.Warnings, w => w.Contains("flip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Export_UnresolvableUvFrame_SkippedWithWarning()
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { UvFrame("missing.png", 0f, 0f, 1f, 1f) },
        });

        var result = GodotSpriteFramesExporter.Export(acls, _ => null);

        Assert.Contains(result.Warnings, w => w.Contains("skipped", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("[sub_resource type=\"AtlasTexture\"", result.Text);
    }
}
