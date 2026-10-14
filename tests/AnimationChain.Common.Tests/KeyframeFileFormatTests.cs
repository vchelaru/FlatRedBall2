using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// File-format side of #1478 phase 1: the new per-frame fields round-trip through .achx and .achj
/// with every value preserved (including ones no runtime understands), are omitted at their
/// defaults, and extra unknown fields in a file don't break loading.
/// </summary>
public class KeyframeFileFormatTests
{
    private static AnimationChainListSave SaveWithFrame(AnimationFrameSave frame)
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "c" };
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);
        return save;
    }

    private static AnimationFrameSave FullyPopulated() => new()
    {
        FrameLength = 0.1f,
        SkipOffsetKeyframe = true,
        SkipColorKeyframe = true,
        OffsetInterpolation = "Cubic",
        OffsetEasing = "InOut",
        ColorInterpolation = "SomethingFromTheFuture",
        ColorEasing = "SidewaysEase",
    };

    private static void AssertPreserved(AnimationFrameSave frame)
    {
        frame.SkipOffsetKeyframe.ShouldBeTrue();
        frame.SkipColorKeyframe.ShouldBeTrue();
        frame.OffsetInterpolation.ShouldBe("Cubic");
        frame.OffsetEasing.ShouldBe("InOut");
        frame.ColorInterpolation.ShouldBe("SomethingFromTheFuture");
        frame.ColorEasing.ShouldBe("SidewaysEase");
    }

    [Fact]
    public void Xml_RoundTrip_PreservesEveryValueIncludingUnknownNames()
    {
        var xml = SaveWithFrame(FullyPopulated()).ToXmlString();

        var frame = AnimationChainListSave.FromString(xml).AnimationChains[0].Frames[0];

        AssertPreserved(frame);
    }

    [Fact]
    public void Json_RoundTrip_PreservesEveryValueIncludingUnknownNames()
    {
        var json = SaveWithFrame(FullyPopulated()).ToJsonString();

        var frame = AnimationChainListSave.FromString(json).AnimationChains[0].Frames[0];

        AssertPreserved(frame);
    }

    [Fact]
    public void Xml_DefaultFrame_WritesNoKeyframeElements()
    {
        var xml = SaveWithFrame(new AnimationFrameSave { FrameLength = 0.1f }).ToXmlString();

        xml.ShouldNotContain("Skip");
        xml.ShouldNotContain("Interpolation");
        xml.ShouldNotContain("Easing");
    }

    [Fact]
    public void Json_DefaultFrame_WritesNoKeyframeProperties()
    {
        var json = SaveWithFrame(new AnimationFrameSave { FrameLength = 0.1f }).ToJsonString();

        json.ShouldNotContain("skip");
        json.ShouldNotContain("nterpolation");
        json.ShouldNotContain("asing");
    }

    [Fact]
    public void Xml_SkipFalse_IsNotWritten()
    {
        var frame = new AnimationFrameSave { SkipOffsetKeyframe = true };

        var xml = SaveWithFrame(frame).ToXmlString();

        xml.ShouldContain("<SkipOffsetKeyframe>true</SkipOffsetKeyframe>");
        xml.ShouldNotContain("SkipColorKeyframe");
    }

    [Fact]
    public void Xml_UsesDocumentedElementNames()
    {
        var xml = SaveWithFrame(FullyPopulated()).ToXmlString();

        xml.ShouldContain("<OffsetInterpolation>Cubic</OffsetInterpolation>");
        xml.ShouldContain("<OffsetEasing>InOut</OffsetEasing>");
        xml.ShouldContain("<ColorInterpolation>SomethingFromTheFuture</ColorInterpolation>");
        xml.ShouldContain("<ColorEasing>SidewaysEase</ColorEasing>");
    }

    [Fact]
    public void Xml_OldFileWithoutFields_LoadsWithDefaults()
    {
        const string xml =
            "<AnimationChainArraySave><AnimationChain><Name>c</Name>" +
            "<Frame><TextureName>a.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "</AnimationChain></AnimationChainArraySave>";

        var frame = AnimationChainListSave.FromString(xml).AnimationChains[0].Frames[0];

        frame.SkipOffsetKeyframe.ShouldBeFalse();
        frame.SkipColorKeyframe.ShouldBeFalse();
        frame.OffsetInterpolation.ShouldBeNull();
        frame.OffsetEasing.ShouldBeNull();
        frame.ColorInterpolation.ShouldBeNull();
        frame.ColorEasing.ShouldBeNull();
    }

    // Old readers survive new files only if unknown fields are skipped. FRB2's own loaders pick fields by
    // name, so extras are ignored; this pins that so a future loader rewrite can't regress it.
    [Fact]
    public void Xml_UnknownFrameElement_IsIgnored()
    {
        const string xml =
            "<AnimationChainArraySave><AnimationChain><Name>c</Name>" +
            "<Frame><TextureName>a.png</TextureName><FrameLength>0.25</FrameLength>" +
            "<SomethingNew>x</SomethingNew><OffsetInterpolation>Linear</OffsetInterpolation></Frame>" +
            "</AnimationChain></AnimationChainArraySave>";

        var frame = AnimationChainListSave.FromString(xml).AnimationChains[0].Frames[0];

        frame.FrameLength.ShouldBe(0.25f);
        frame.OffsetInterpolation.ShouldBe("Linear");
    }

    [Fact]
    public void Json_UnknownFrameProperty_IsIgnored()
    {
        const string json =
            "{\"animationChains\":[{\"name\":\"c\",\"frames\":[" +
            "{\"textureName\":\"a.png\",\"frameLength\":0.25,\"somethingNew\":{\"a\":1},\"offsetInterpolation\":\"Linear\"}]}]}";

        var frame = AnimationChainListSave.FromString(json).AnimationChains[0].Frames[0];

        frame.FrameLength.ShouldBe(0.25f);
        frame.OffsetInterpolation.ShouldBe("Linear");
    }

    [Fact]
    public void Xml_SaveLoadSave_IsStable()
    {
        var first = SaveWithFrame(FullyPopulated()).ToXmlString();

        var second = AnimationChainListSave.FromString(first).ToXmlString();

        second.ShouldBe(first);
    }

    [Fact]
    public void Json_SaveLoadSave_IsStable()
    {
        var first = SaveWithFrame(FullyPopulated()).ToJsonString();

        var second = AnimationChainListSave.FromString(first).ToJsonString();

        second.ShouldBe(first);
    }
}
