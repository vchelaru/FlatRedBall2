using AnimationEditor.Core.Export;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests.Export;

public class GodotSpriteFramesExporterTests
{
    private static AnimationFrameSave PixelFrame(string textureName, int x, int y, int size, float frameLength) =>
        new()
        {
            TextureName = textureName,
            LeftCoordinate = x,
            TopCoordinate = y,
            RightCoordinate = x + size,
            BottomCoordinate = y + size,
            FrameLength = frameLength,
        };

    private static AnimationChainListSave SingleChain(params AnimationFrameSave[] frames)
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.AddRange(frames);
        acls.AnimationChains.Add(chain);
        return acls;
    }

    [Fact]
    public void Export_MultipleChainsTexturesAndDurations_MatchesGoldenTres()
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames =
            {
                PixelFrame("characters.png", 0, 32, 32, 0.1f),
                PixelFrame("characters.png", 32, 32, 32, 0.1f),
                // Backslash path from a Windows-authored .achx; Godot paths use '/'.
                PixelFrame(@"items\coin.png", 0, 0, 16, 0.2f),
            },
        });
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Die",
            Loop = false,
            Frames = { PixelFrame("characters.png", 0, 64, 32, 0.25f) },
        });
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Empty" });

        var result = GodotSpriteFramesExporter.Export(acls, _ => null);

        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "godot-spriteframes-expected.tres"))
            .Replace("\r\n", "\n");
        result.Text.ShouldBe(expected);
        result.Warnings.ShouldBeEmpty();
        result.ReferencedTextures.ShouldBe(new[] { "characters.png", @"items\coin.png" });
    }

    [Fact]
    public void Export_UnevenDurations_SpeedFromShortestFrameAndDurationsRelativeToIt()
    {
        // Godot plays a frame for duration / speed seconds, so 0.05s and 0.15s frames must
        // come out as speed 20 with relative durations 1 and 3.
        var acls = SingleChain(PixelFrame("a.png", 0, 0, 8, 0.15f), PixelFrame("a.png", 8, 0, 8, 0.05f));

        var text = GodotSpriteFramesExporter.Export(acls, _ => null).Text;

        text.ShouldContain("\"speed\": 20.0");
        text.ShouldContain("\"duration\": 3.0,\n\"texture\": SubResource(\"AtlasTexture_1\")");
        text.ShouldContain("\"duration\": 1.0,\n\"texture\": SubResource(\"AtlasTexture_2\")");
    }

    [Fact]
    public void Export_FlippedFrame_KeepsFrameAndWarns()
    {
        var frame = PixelFrame("a.png", 0, 0, 8, 0.1f);
        frame.FlipHorizontal = true;

        var result = GodotSpriteFramesExporter.Export(SingleChain(frame), _ => null);

        result.Text.ShouldContain("region = Rect2(0, 0, 8, 8)");
        result.Warnings.ShouldContain(w => w.Contains("flip"));
    }

    [Fact]
    public void Export_ZeroLengthFrame_UsesGodotMinimumAndWarns()
    {
        var acls = SingleChain(PixelFrame("a.png", 0, 0, 8, 0.1f), PixelFrame("a.png", 8, 0, 8, 0f));

        var result = GodotSpriteFramesExporter.Export(acls, _ => null);

        result.Text.ShouldContain("\"duration\": 0.01,");
        result.Warnings.ShouldContain(w => w.Contains("zero duration"));
    }

    [Fact]
    public void Export_UvFrameWithUnreadableTexture_SkipsFrameAndWarns()
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        acls.AnimationChains.Add(new AnimationChainSave
        {
            Name = "Walk",
            Frames = { new AnimationFrameSave { TextureName = "missing.png", FrameLength = 0.1f } },
        });

        var result = GodotSpriteFramesExporter.Export(acls, _ => null);

        result.Text.ShouldNotContain("AtlasTexture");
        result.Warnings.ShouldContain(w => w.Contains("missing.png"));
    }

    [Fact]
    public void Export_DuplicateChainNames_RenamesSecondAndWarns()
    {
        var acls = SingleChain(PixelFrame("a.png", 0, 0, 8, 0.1f));
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });

        var result = GodotSpriteFramesExporter.Export(acls, _ => null);

        // Godot keys animations by name, so a duplicate would silently replace the first.
        result.Text.ShouldContain("\"name\": &\"Walk\"");
        result.Text.ShouldContain("\"name\": &\"Walk_2\"");
        result.Warnings.ShouldContain(w => w.Contains("Walk_2"));
    }

    [Fact]
    public void Export_ChainNameWithQuote_IsEscaped()
    {
        var acls = SingleChain();
        acls.AnimationChains[0].Name = "Say \"hi\"";

        var text = GodotSpriteFramesExporter.Export(acls, _ => null).Text;

        text.ShouldContain("\"name\": &\"Say \\\"hi\\\"\"");
    }
}
