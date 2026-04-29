using System;
using System.IO;
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
            "      <ShapesSave>" +
            "        <AARectSaves>" +
            "          <AARectSave><Name>Sword</Name><X>5</X><Y>0</Y><ScaleX>15</ScaleX><ScaleY>5</ScaleY></AARectSave>" +
            "        </AARectSaves>" +
            "      </ShapesSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.AnimationChains[0].Frames[0].ShapesSave.ShouldNotBeNull();
        save.AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.Count.ShouldBe(1);
        save.AnimationChains[0].Frames[0].ShapesSave!.AARectSaves[0].Name.ShouldBe("Sword");
    }

    [Fact]
    public void FromFile_CircleShape_DeserializesFields()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Hit</Name>" +
            "    <Frame><TextureName>a.png</TextureName><FrameLength>0.1</FrameLength>" +
            "      <ShapesSave>" +
            "        <CircleSaves>" +
            "          <CircleSave><Name>Blast</Name><X>3</X><Y>4</Y><Radius>12</Radius></CircleSave>" +
            "        </CircleSaves>" +
            "      </ShapesSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        var circle = save.AnimationChains[0].Frames[0].ShapesSave!.CircleSaves[0];
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
            "      <ShapesSave>" +
            "        <PolygonSaves>" +
            "          <PolygonSave><Name>Arc</Name><X>1</X><Y>2</Y>" +
            "            <Points>" +
            "              <Vector2Save><X>0</X><Y>0</Y></Vector2Save>" +
            "              <Vector2Save><X>10</X><Y>5</Y></Vector2Save>" +
            "              <Vector2Save><X>0</X><Y>10</Y></Vector2Save>" +
            "            </Points>" +
            "          </PolygonSave>" +
            "        </PolygonSaves>" +
            "      </ShapesSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        var poly = save.AnimationChains[0].Frames[0].ShapesSave!.PolygonSaves[0];
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
            "      <ShapesSave>" +
            "        <AARectSaves>" +
            "          <AARectSave><Name></Name><ScaleX>5</ScaleX><ScaleY>5</ScaleY></AARectSave>" +
            "        </AARectSaves>" +
            "      </ShapesSave>" +
            "    </Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        Should.Throw<InvalidOperationException>(() => save.ToAnimationChainList(null!));
    }
}
