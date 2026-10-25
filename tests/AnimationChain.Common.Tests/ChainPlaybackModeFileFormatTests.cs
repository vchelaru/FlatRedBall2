using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// The chain's PingPong and Reverse flags (#1538) in .achx/.achj: written only when true, so older readers (Gum,
/// FRB1) ignore them and files that don't set them are unchanged.
/// </summary>
public class ChainPlaybackModeFileFormatTests
{
    private static AnimationChainListSave SaveWith(bool pingPong, bool reverse)
    {
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Walk", PingPong = pingPong, Reverse = reverse });
        return save;
    }

    [Fact]
    public void ToXmlString_PingPongAndReverseTrue_RoundTrip()
    {
        var xml = SaveWith(pingPong: true, reverse: true).ToXmlString();
        var chain = AnimationChainListSave.FromString(xml).AnimationChains.Single();

        xml.ShouldContain("<PingPong>true</PingPong>");
        xml.ShouldContain("<Reverse>true</Reverse>");
        chain.PingPong.ShouldBeTrue();
        chain.Reverse.ShouldBeTrue();
    }

    [Fact]
    public void ToJsonString_PingPongAndReverseTrue_RoundTrip()
    {
        var json = SaveWith(pingPong: true, reverse: true).ToJsonString();
        var chain = AnimationChainListSave.FromJsonString(json).AnimationChains.Single();

        json.ShouldContain("\"pingPong\": true");
        json.ShouldContain("\"reverse\": true");
        chain.PingPong.ShouldBeTrue();
        chain.Reverse.ShouldBeTrue();
    }

    [Fact]
    public void ToXmlStringAndToJsonString_FlagsFalse_OmitThem()
    {
        var save = SaveWith(pingPong: false, reverse: false);

        save.ToXmlString().ShouldNotContain("PingPong");
        save.ToXmlString().ShouldNotContain("Reverse");
        save.ToJsonString().ShouldNotContain("pingPong");
        save.ToJsonString().ShouldNotContain("reverse");
    }

    [Theory]
    [InlineData("PreChangePolygon.achx")]
    [InlineData("PreChangePolygon.achj")]
    public void Load_FileWrittenBeforeTheFlags_DefaultsThemFalse_AndResavesUnchanged(string fixture)
    {
        // The fixtures predate #1538, so they carry no PingPong/Reverse field.
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
        bool json = fixture.EndsWith(".achj");
        var loaded = AnimationChainListSave.FromFile(source);
        var resaved = Path.Combine(Path.GetTempPath(), "PlaybackMode-" + Guid.NewGuid().ToString("N") + Path.GetExtension(fixture));
        try
        {
            if (json) loaded.SaveJson(resaved); else loaded.Save(resaved);

            loaded.AnimationChains.ShouldAllBe(c => !c.PingPong && !c.Reverse);
            if (json) // the JSON writer's newline follows the platform
                File.ReadAllText(resaved).Replace("\r\n", "\n").ShouldBe(File.ReadAllText(source).Replace("\r\n", "\n"));
            else
                File.ReadAllBytes(resaved).ShouldBe(File.ReadAllBytes(source));
        }
        finally { File.Delete(resaved); }
    }
}
