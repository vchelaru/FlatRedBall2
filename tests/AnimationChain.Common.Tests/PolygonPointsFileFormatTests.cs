using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// File-format side of polygon point interpolation (#1484): the Points group fields round-trip through .achx and
/// .achj with every value preserved, are omitted at their defaults so existing files stay byte-identical, and
/// only polygons carry them.
/// </summary>
public class PolygonPointsFileFormatTests
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

    private static PolygonSave FullPolygon()
    {
        var poly = new PolygonSave
        {
            Name = "P", SkipPointsKeyframe = true, PointsInterpolation = "FutureCurve", PointsEasing = "Sideways",
        };
        poly.Points.Add(new Vector2Save { X = 0, Y = 0 });
        poly.Points.Add(new Vector2Save { X = 1, Y = 0 });
        poly.Points.Add(new Vector2Save { X = 1, Y = 1 });
        return poly;
    }

    private static void AssertPreserved(AnimationChainListSave loaded)
    {
        var p = loaded.AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves.Single();
        p.SkipPointsKeyframe.ShouldBeTrue();
        p.PointsInterpolation.ShouldBe("FutureCurve");
        p.PointsEasing.ShouldBe("Sideways");
        p.Points.Count.ShouldBe(3);
    }

    [Fact]
    public void Xml_RoundTrip_PreservesEveryPointsValue()
    {
        AssertPreserved(AnimationChainListSave.FromString(SaveWith(FullPolygon()).ToXmlString()));
    }

    [Fact]
    public void Json_RoundTrip_PreservesEveryPointsValue()
    {
        AssertPreserved(AnimationChainListSave.FromString(SaveWith(FullPolygon()).ToJsonString()));
    }

    [Fact]
    public void Xml_DefaultPolygon_WritesNoPointsKeyframeElements()
    {
        var xml = SaveWith(new PolygonSave { Name = "P" }).ToXmlString();

        xml.ShouldNotContain("Points" + "Interpolation");
        xml.ShouldNotContain("PointsEasing");
        xml.ShouldNotContain("SkipPointsKeyframe");
    }

    [Fact]
    public void Json_DefaultPolygon_WritesNoPointsKeyframeProperties()
    {
        var json = SaveWith(new PolygonSave { Name = "P" }).ToJsonString();

        json.ShouldNotContain("skipPointsKeyframe");
        json.ShouldNotContain("pointsInterpolation");
        json.ShouldNotContain("pointsEasing");
    }

    [Fact]
    public void Xml_PolygonFromOldFileWithoutFields_LoadsAsSnapKeyframe()
    {
        var xml = SaveWith(new PolygonSave { Name = "P" }).ToXmlString();

        var poly = AnimationChainListSave.FromString(xml).AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves.Single();

        poly.SkipPointsKeyframe.ShouldBeFalse();
        poly.PointsInterpolation.ShouldBeNull();
        poly.PointsEasing.ShouldBeNull();
    }

    [Fact]
    public void Json_PolygonFromOldFileWithoutFields_LoadsAsSnapKeyframe()
    {
        var json = SaveWith(new PolygonSave { Name = "P" }).ToJsonString();

        var poly = AnimationChainListSave.FromString(json).AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves.Single();

        poly.SkipPointsKeyframe.ShouldBeFalse();
        poly.PointsInterpolation.ShouldBeNull();
        poly.PointsEasing.ShouldBeNull();
    }

    [Fact]
    public void Xml_SecondSave_IsByteIdentical()
    {
        var first = SaveWith(FullPolygon()).ToXmlString();

        AnimationChainListSave.FromString(first).ToXmlString().ShouldBe(first);
    }

    [Fact]
    public void Json_SecondSave_IsByteIdentical()
    {
        var first = SaveWith(FullPolygon()).ToJsonString();

        AnimationChainListSave.FromString(first).ToJsonString().ShouldBe(first);
    }

    [Fact]
    public void Xml_RectanglesAndCircles_NeverWritePointsFields()
    {
        var xml = SaveWith(new AARectSave { Name = "R", OffsetInterpolation = "Linear" }, new CircleSave { Name = "C" }).ToXmlString();

        xml.ShouldNotContain("Points" + "Interpolation");
        xml.ShouldNotContain("SkipPointsKeyframe");
    }
}
