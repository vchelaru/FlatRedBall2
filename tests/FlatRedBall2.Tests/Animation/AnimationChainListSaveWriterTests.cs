using System.IO;
using System.Linq;
using System.Xml.Linq;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Verifies that AnimationChainListSave.Save emits FRB1's XML element dialect — same
// element names FRB1 tooling produces — so .achx files written by FRB2 are readable by
// any consumer (including legacy AE) and round-trip cleanly through the FRB2 reader once
// commit 3 retargets it to the FRB1 dialect.
public class AnimationChainListSaveWriterTests
{
    private static XDocument SaveAndParse(AnimationChainListSave save)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            save.Save(tempPath);
            return XDocument.Load(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Save_AARect_EmitsAxisAlignedRectangleSaveElementName()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.AARectSaves.Add(new AARectSave { Name = "Hit", X = 1, Y = 2, ScaleX = 3, ScaleY = 4 });
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);

        var doc = SaveAndParse(save);

        var rect = doc.Descendants("AxisAlignedRectangleSave").Single();
        rect.Element("Name")!.Value.ShouldBe("Hit");
        rect.Element("ScaleX")!.Value.ShouldBe("3");
    }

    [Fact]
    public void Save_Circle_EmitsCircleSaveInsideShapeCollectionSave()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.CircleSaves.Add(new CircleSave { Name = "Origin", X = 5, Y = 6, Radius = 7 });
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);

        var doc = SaveAndParse(save);

        var circle = doc.Descendants("ShapeCollectionSave").Single().Descendants("CircleSave").Single();
        circle.Element("Radius")!.Value.ShouldBe("7");
    }

    [Fact]
    public void Save_FlipHorizontalFalse_OmitsElement()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "X" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, FlipHorizontal = false });
        save.AnimationChains.Add(chain);

        var doc = SaveAndParse(save);

        doc.Descendants("FlipHorizontal").ShouldBeEmpty();
    }

    [Fact]
    public void Save_FlipHorizontalTrue_EmitsElement()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "X" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, FlipHorizontal = true });
        save.AnimationChains.Add(chain);

        var doc = SaveAndParse(save);

        doc.Descendants("FlipHorizontal").Single().Value.ShouldBe("true");
    }

    [Fact]
    public void Save_Polygon_EmitsPointsWithVector2Save()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "P" };
        var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        var poly = new PolygonSave { Name = "Shape", X = 0, Y = 0 };
        poly.Points.Add(new Vector2Save { X = 1, Y = 2 });
        poly.Points.Add(new Vector2Save { X = 3, Y = 4 });
        frame.ShapesSave.PolygonSaves.Add(poly);
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);

        var doc = SaveAndParse(save);

        var points = doc.Descendants("PolygonSave").Single().Element("Points")!.Elements("Vector2Save").ToList();
        points.Count.ShouldBe(2);
        points[0].Element("X")!.Value.ShouldBe("1");
        points[1].Element("Y")!.Value.ShouldBe("4");
    }

    [Fact]
    public void Save_RootElement_IsAnimationChainArraySave()
    {
        var save = new AnimationChainListSave();

        var doc = SaveAndParse(save);

        doc.Root!.Name.LocalName.ShouldBe("AnimationChainArraySave");
    }

    [Fact]
    public void Save_TopLevelFields_EmittedInFrb1Order()
    {
        var save = new AnimationChainListSave
        {
            FileRelativeTextures = true,
            TimeMeasurementUnit = TimeMeasurementUnit.Second,
            CoordinateType = TextureCoordinateType.Pixel,
        };

        var doc = SaveAndParse(save);

        var first = doc.Root!.Elements().Take(3).Select(e => e.Name.LocalName).ToList();
        first.ShouldBe(new[] { "FileRelativeTextures", "TimeMeasurementUnit", "CoordinateType" });
        doc.Root.Element("CoordinateType")!.Value.ShouldBe("Pixel");
    }
}
