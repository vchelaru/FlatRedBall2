using System;
using System.IO;
using System.Linq;
using System.Text;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Verifies that AnimationChainListSave.FromFile reads .achx bytes through an injectable seam
// — proof that the engine no longer hard-codes a File.IO path and can route reads through
// TitleContainer (required for KNI Blazor / WASM, where there is no filesystem).
public class AnimationChainListSaveLoadingTests
{
    [Fact]
    public void FromFile_StreamProvider_ReceivesProvidedPathAndProducesSave()
    {
        string requestedPath = "Content/Animations/ShmupSpace.achx";
        string? observedPath = null;

        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Walk</Name>" +
            "    <Frame><TextureName>walk.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile(requestedPath, path =>
        {
            observedPath = path;
            return new MemoryStream(Encoding.UTF8.GetBytes(xml));
        });

        observedPath.ShouldBe(requestedPath);
        save.AnimationChains.Count.ShouldBe(1);
        save.AnimationChains[0].Name.ShouldBe("Walk");
    }

    [Fact]
    public void FromFile_FrameWithShapeCollection_DeserializesRectangleEntry()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Attack</Name>" +
            "    <Frame><TextureName>a.png</TextureName><FrameLength>0.1</FrameLength>" +
            "      <ShapeCollectionSave>" +
            "        <AxisAlignedRectangleSaves>" +
            "          <AxisAlignedRectangleSave><Name>Sword</Name><X>5</X><Y>0</Y><ScaleX>15</ScaleX><ScaleY>5</ScaleY></AxisAlignedRectangleSave>" +
            "        </AxisAlignedRectangleSaves>" +
            "      </ShapeCollectionSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.AnimationChains[0].Frames[0].ShapesSave.ShouldNotBeNull();
        save.AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.Count().ShouldBe(1);
        save.AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.First().Name.ShouldBe("Sword");
    }

    // ToXmlString is the in-memory companion to Save(path): the Animation Editor clipboard
    // serializes copied frames/chains through it, so it must round-trip per-frame shapes.
    [Fact]
    public void ToXmlString_RoundTripsFrameShapesViaFromString()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Attack" };
        var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "Sword", X = 5f, Y = 0f, ScaleX = 15f, ScaleY = 5f });
        chain.Frames.Add(frame);
        save.AnimationChains.Add(chain);

        var roundTripped = AnimationChainListSave.FromString(save.ToXmlString());

        var rect = roundTripped.AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.Single();
        rect.Name.ShouldBe("Sword");
        rect.ScaleX.ShouldBe(15f);
    }

    [Fact]
    public void FromFile_CircleShape_DeserializesFields()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Hit</Name>" +
            "    <Frame><TextureName>a.png</TextureName><FrameLength>0.1</FrameLength>" +
            "      <ShapeCollectionSave>" +
            "        <CircleSaves>" +
            "          <CircleSave><Name>Blast</Name><X>3</X><Y>4</Y><Radius>12</Radius></CircleSave>" +
            "        </CircleSaves>" +
            "      </ShapeCollectionSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        var circle = save.AnimationChains[0].Frames[0].ShapesSave!.CircleSaves.First();
        circle.Name.ShouldBe("Blast");
        circle.X.ShouldBe(3f);
        circle.Y.ShouldBe(4f);
        circle.Radius.ShouldBe(12f);
    }

    [Fact]
    public void FromFile_FrameCoordinatesAndFlip_DeserializesAllFields()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <TimeMeasurementUnit>Millisecond</TimeMeasurementUnit>" +
            "  <CoordinateType>Pixel</CoordinateType>" +
            "  <AnimationChain><Name>Run</Name>" +
            "    <Frame>" +
            "      <TextureName>run.png</TextureName>" +
            "      <FrameLength>100</FrameLength>" +
            "      <LeftCoordinate>10</LeftCoordinate>" +
            "      <RightCoordinate>42</RightCoordinate>" +
            "      <TopCoordinate>5</TopCoordinate>" +
            "      <BottomCoordinate>37</BottomCoordinate>" +
            "      <FlipHorizontal>true</FlipHorizontal>" +
            "      <FlipVertical>true</FlipVertical>" +
            "      <RelativeX>2.5</RelativeX>" +
            "      <RelativeY>-1.5</RelativeY>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
        save.CoordinateType.ShouldBe(TextureCoordinateType.Pixel);

        var frame = save.AnimationChains[0].Frames[0];
        frame.FrameLength.ShouldBe(100f);
        frame.LeftCoordinate.ShouldBe(10f);
        frame.RightCoordinate.ShouldBe(42f);
        frame.TopCoordinate.ShouldBe(5f);
        frame.BottomCoordinate.ShouldBe(37f);
        frame.FlipHorizontal.ShouldBeTrue();
        frame.FlipVertical.ShouldBeTrue();
        frame.RelativeX.ShouldBe(2.5f);
        frame.RelativeY.ShouldBe(-1.5f);
    }

    [Fact]
    public void FromFile_MultipleChains_PreservesOrder()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Idle</Name>" +
            "    <Frame><TextureName>i0.png</TextureName><FrameLength>0.2</FrameLength></Frame>" +
            "    <Frame><TextureName>i1.png</TextureName><FrameLength>0.3</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "  <AnimationChain><Name>Walk</Name>" +
            "    <Frame><TextureName>w0.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.AnimationChains.Count.ShouldBe(2);
        save.AnimationChains[0].Name.ShouldBe("Idle");
        save.AnimationChains[0].Frames.Count.ShouldBe(2);
        save.AnimationChains[0].Frames[0].TextureName.ShouldBe("i0.png");
        save.AnimationChains[0].Frames[1].TextureName.ShouldBe("i1.png");
        save.AnimationChains[1].Name.ShouldBe("Walk");
        save.AnimationChains[1].Frames.Count.ShouldBe(1);
    }

    [Fact]
    public void FromFile_PolygonShape_DeserializesPoints()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Slash</Name>" +
            "    <Frame><TextureName>s.png</TextureName><FrameLength>0.1</FrameLength>" +
            "      <ShapeCollectionSave>" +
            "        <PolygonSaves>" +
            "          <PolygonSave><Name>Arc</Name><X>1</X><Y>2</Y>" +
            "            <Points>" +
            "              <Vector2Save><X>0</X><Y>0</Y></Vector2Save>" +
            "              <Vector2Save><X>10</X><Y>5</Y></Vector2Save>" +
            "              <Vector2Save><X>0</X><Y>10</Y></Vector2Save>" +
            "            </Points>" +
            "          </PolygonSave>" +
            "        </PolygonSaves>" +
            "      </ShapeCollectionSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        var poly = save.AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves.First();
        poly.Name.ShouldBe("Arc");
        poly.X.ShouldBe(1f);
        poly.Y.ShouldBe(2f);
        poly.Points.Count.ShouldBe(3);
        poly.Points[1].X.ShouldBe(10f);
        poly.Points[1].Y.ShouldBe(5f);
    }

    [Fact]
    public void ToAnimationChainList_ShapeWithEmptyName_Throws()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Attack</Name>" +
            "    <Frame><FrameLength>0.1</FrameLength>" +
            "      <ShapeCollectionSave>" +
            "        <AxisAlignedRectangleSaves>" +
            "          <AxisAlignedRectangleSave><Name></Name><ScaleX>5</ScaleX><ScaleY>5</ScaleY></AxisAlignedRectangleSave>" +
            "        </AxisAlignedRectangleSaves>" +
            "      </ShapeCollectionSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        Should.Throw<InvalidOperationException>(() => save.ToAnimationChainList(null!));
    }

    // ─── FromStream ──────────────────────────────────────────────────────────────

    private static readonly string SimpleXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
        "<AnimationChainArraySave>" +
        "  <TimeMeasurementUnit>Second</TimeMeasurementUnit>" +
        "  <CoordinateType>UV</CoordinateType>" +
        "  <AnimationChain><Name>Walk</Name>" +
        "    <Frame><TextureName>walk.png</TextureName><FrameLength>0.1</FrameLength>" +
        "      <LeftCoordinate>0</LeftCoordinate><RightCoordinate>0.25</RightCoordinate>" +
        "      <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>" +
        "    </Frame>" +
        "  </AnimationChain>" +
        "  <AnimationChain><Name>Idle</Name>" +
        "    <Frame><TextureName>idle.png</TextureName><FrameLength>0.5</FrameLength></Frame>" +
        "  </AnimationChain>" +
        "</AnimationChainArraySave>";

    [Fact]
    public void FromStream_ParsesChainNames()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleXml));
        var save = AnimationChainListSave.FromStream(stream);
        save.AnimationChains.Count.ShouldBe(2);
        save.AnimationChains[0].Name.ShouldBe("Walk");
        save.AnimationChains[1].Name.ShouldBe("Idle");
    }

    [Fact]
    public void FromStream_FileNameIsEmpty()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleXml));
        var save = AnimationChainListSave.FromStream(stream);
        save.FileName.ShouldBe(string.Empty);
    }

    [Fact]
    public void FromStream_ParsesFrameCoordinates()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleXml));
        var save = AnimationChainListSave.FromStream(stream);
        var f = save.AnimationChains[0].Frames[0];
        f.LeftCoordinate.ShouldBe(0f);
        f.RightCoordinate.ShouldBe(0.25f);
        f.TopCoordinate.ShouldBe(0f);
        f.BottomCoordinate.ShouldBe(1f);
    }

    [Fact]
    public void FromStream_ProducesEquivalentResultToFromFile()
    {
        var fromFile = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(SimpleXml)));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SimpleXml));
        var fromStream = AnimationChainListSave.FromStream(stream);

        fromStream.AnimationChains.Count.ShouldBe(fromFile.AnimationChains.Count);
        fromStream.AnimationChains[0].Name.ShouldBe(fromFile.AnimationChains[0].Name);
        fromStream.TimeMeasurementUnit.ShouldBe(fromFile.TimeMeasurementUnit);
        fromStream.CoordinateType.ShouldBe(fromFile.CoordinateType);
    }

    // ─── FromString ──────────────────────────────────────────────────────────────

    [Fact]
    public void FromString_ParsesChainNames()
    {
        var save = AnimationChainListSave.FromString(SimpleXml);
        save.AnimationChains.Count.ShouldBe(2);
        save.AnimationChains[0].Name.ShouldBe("Walk");
        save.AnimationChains[1].Name.ShouldBe("Idle");
    }

    [Fact]
    public void FromString_FileNameIsEmpty()
    {
        var save = AnimationChainListSave.FromString(SimpleXml);
        save.FileName.ShouldBe(string.Empty);
    }

    [Fact]
    public void FromString_ParsesFrameCount()
    {
        var save = AnimationChainListSave.FromString(SimpleXml);
        save.AnimationChains[0].Frames.Count.ShouldBe(1);
        save.AnimationChains[1].Frames.Count.ShouldBe(1);
    }

    [Fact]
    public void FromString_ProducesEquivalentResultToFromFile()
    {
        var fromFile = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(SimpleXml)));
        var fromString = AnimationChainListSave.FromString(SimpleXml);

        fromString.AnimationChains.Count.ShouldBe(fromFile.AnimationChains.Count);
        fromString.AnimationChains[0].Name.ShouldBe(fromFile.AnimationChains[0].Name);
        fromString.TimeMeasurementUnit.ShouldBe(fromFile.TimeMeasurementUnit);
        fromString.CoordinateType.ShouldBe(fromFile.CoordinateType);
    }
}
