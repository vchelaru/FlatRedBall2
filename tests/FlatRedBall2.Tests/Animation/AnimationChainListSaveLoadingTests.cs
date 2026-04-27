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

        save.AnimationChains[0].Frames[0].ShapeCollectionSave.ShouldNotBeNull();
        save.AnimationChains[0].Frames[0].ShapeCollectionSave!.AxisAlignedRectangleSaves.Count.ShouldBe(1);
        save.AnimationChains[0].Frames[0].ShapeCollectionSave!.AxisAlignedRectangleSaves[0].Name.ShouldBe("Sword");
    }

    [Fact]
    public void ToAnimationChainList_ShapeWithEmptyName_Throws()
    {
        // Drives the "unnamed frame shape rejected at load" rule.
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

        // ContentManagerService is null because no textures are referenced; the empty Name
        // check fires before texture loading.
        Should.Throw<InvalidOperationException>(() => save.ToAnimationChainList(null!));
    }
}
