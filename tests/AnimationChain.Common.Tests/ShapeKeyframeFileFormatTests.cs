using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// File-format side of shape keyframes (#1478 final phase): the per-shape Offset and Size fields
/// round-trip through .achx and .achj with every value preserved (including names no runtime
/// understands), and are omitted at their defaults so existing files stay byte-identical.
/// </summary>
public class ShapeKeyframeFileFormatTests
{
    private static AnimationChainListSave SaveWith(params ShapeSave[] shapes)
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "c" };
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        foreach (var s in shapes) frame.ShapesSave.Add(s);
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);
        return save;
    }

    private static AARectSave FullRect() => new()
    {
        Name = "R", SkipOffsetKeyframe = true, OffsetInterpolation = "Cubic", OffsetEasing = "InOut",
        SkipSizeKeyframe = true, SizeInterpolation = "FutureCurve", SizeEasing = "Sideways",
    };

    private static CircleSave FullCircle() => new()
    {
        Name = "C", SkipOffsetKeyframe = true, OffsetInterpolation = "Back", OffsetEasing = "In",
        SkipSizeKeyframe = true, SizeInterpolation = "Bounce", SizeEasing = "Out",
    };

    private static PolygonSave FullPolygon() => new()
    {
        Name = "P", SkipOffsetKeyframe = true, OffsetInterpolation = "Linear", OffsetEasing = "Out",
    };

    private static void AssertPreserved(AnimationChainListSave loaded)
    {
        var shapes = loaded.AnimationChains[0].Frames[0].ShapesSave!;

        var r = shapes.AARectSaves.Single();
        r.SkipOffsetKeyframe.ShouldBeTrue();
        r.OffsetInterpolation.ShouldBe("Cubic");
        r.OffsetEasing.ShouldBe("InOut");
        r.SkipSizeKeyframe.ShouldBeTrue();
        r.SizeInterpolation.ShouldBe("FutureCurve");
        r.SizeEasing.ShouldBe("Sideways");

        var c = shapes.CircleSaves.Single();
        c.SkipOffsetKeyframe.ShouldBeTrue();
        c.OffsetInterpolation.ShouldBe("Back");
        c.OffsetEasing.ShouldBe("In");
        c.SkipSizeKeyframe.ShouldBeTrue();
        c.SizeInterpolation.ShouldBe("Bounce");
        c.SizeEasing.ShouldBe("Out");

        var p = shapes.PolygonSaves.Single();
        p.SkipOffsetKeyframe.ShouldBeTrue();
        p.OffsetInterpolation.ShouldBe("Linear");
        p.OffsetEasing.ShouldBe("Out");
    }

    [Fact]
    public void Xml_RoundTrip_PreservesEveryShapeKeyframeValue()
    {
        var xml = SaveWith(FullRect(), FullCircle(), FullPolygon()).ToXmlString();

        AssertPreserved(AnimationChainListSave.FromString(xml));
    }

    [Fact]
    public void Json_RoundTrip_PreservesEveryShapeKeyframeValue()
    {
        var json = SaveWith(FullRect(), FullCircle(), FullPolygon()).ToJsonString();

        AssertPreserved(AnimationChainListSave.FromString(json));
    }

    [Fact]
    public void Xml_DefaultShapes_WriteNoKeyframeElements()
    {
        var xml = SaveWith(new AARectSave { Name = "R" }, new CircleSave { Name = "C" }, new PolygonSave { Name = "P" }).ToXmlString();

        xml.ShouldNotContain("Skip");
        xml.ShouldNotContain("Interpolation");
        xml.ShouldNotContain("Easing");
    }

    [Fact]
    public void Json_DefaultShapes_WriteNoKeyframeProperties()
    {
        var json = SaveWith(new AARectSave { Name = "R" }, new CircleSave { Name = "C" }, new PolygonSave { Name = "P" }).ToJsonString();

        json.ShouldNotContain("skip");
        json.ShouldNotContain("nterpolation");
        json.ShouldNotContain("asing");
    }

    [Fact]
    public void Xml_ShapeFromOldFileWithoutFields_LoadsAsSnapKeyframe()
    {
        var xml = SaveWith(new AARectSave { Name = "R" }).ToXmlString();

        var rect = AnimationChainListSave.FromString(xml).AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.Single();

        rect.SkipOffsetKeyframe.ShouldBeFalse();
        rect.SkipSizeKeyframe.ShouldBeFalse();
        rect.OffsetInterpolation.ShouldBeNull();
        rect.SizeInterpolation.ShouldBeNull();
    }

    [Fact]
    public void Xml_SecondSave_IsByteIdentical()
    {
        var first = SaveWith(FullRect(), FullCircle(), FullPolygon()).ToXmlString();

        var second = AnimationChainListSave.FromString(first).ToXmlString();

        second.ShouldBe(first);
    }
}
