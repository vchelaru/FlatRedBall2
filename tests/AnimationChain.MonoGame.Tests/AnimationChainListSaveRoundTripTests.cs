using System.Text;
using FlatRedBall.AnimationChain;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationChain.MonoGame.Tests;

// #535 follow-up: AnimationChain.MonoGame/KNI silently dropped FlipDiagonal and the per-frame
// Red/Green/Blue/Alpha/ColorOperation fields on load, since its AnimationFrameSave never had
// them (the main engine's diagonal-flip feature, #592, only ever touched
// FlatRedBall2.AnimationEditorCommon). These tests cover the round-trip parity fix.
public class AnimationChainListSaveRoundTripTests
{
    private static Func<string, Stream> XmlStream(string xml) =>
        _ => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    private const string FullFieldsAchx = """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>false</FileRelativeTextures>
          <TimeMeasurementUnit>Second</TimeMeasurementUnit>
          <CoordinateType>UV</CoordinateType>
          <AnimationChain>
            <Name>Flash</Name>
            <Frame>
              <FlipDiagonal>true</FlipDiagonal>
              <TextureName>t.png</TextureName>
              <FrameLength>0.1</FrameLength>
              <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
              <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
              <Red>200</Red>
              <Green>100</Green>
              <Blue>50</Blue>
              <Alpha>255</Alpha>
              <ColorOperation>Add</ColorOperation>
            </Frame>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    [Fact]
    public void ParseFrame_FlipDiagonalAndColorChannels_ParsesAllValues()
    {
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream(FullFieldsAchx));
        var frame = save.AnimationChains[0].Frames[0];

        Assert.True(frame.FlipDiagonal);
        Assert.Equal(200, frame.Red);
        Assert.Equal(100, frame.Green);
        Assert.Equal(50, frame.Blue);
        Assert.Equal(255, frame.Alpha);
        Assert.Equal(ColorOperation.Add, frame.ColorOperation);
    }

    [Fact]
    public void Save_FlipDiagonalAndColorChannelsSet_RoundTripsThroughReparse()
    {
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream(FullFieldsAchx));
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            save.Save(tempPath);
            var reloaded = AnimationChainListSave.FromFile(tempPath);
            var frame = reloaded.AnimationChains[0].Frames[0];

            Assert.True(frame.FlipDiagonal);
            Assert.Equal(200, frame.Red);
            Assert.Equal(100, frame.Green);
            Assert.Equal(50, frame.Blue);
            Assert.Equal(255, frame.Alpha);
            Assert.Equal(ColorOperation.Add, frame.ColorOperation);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Save_FrameWithDefaultsOnly_OmitsFlipDiagonalAndColorElements()
    {
        const string minimalAchx = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>false</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Idle</Name>
                <Frame>
                  <TextureName>t.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream(minimalAchx));
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            save.Save(tempPath);
            var xml = File.ReadAllText(tempPath);

            Assert.DoesNotContain("FlipDiagonal", xml);
            Assert.DoesNotContain("<Red>", xml);
            Assert.DoesNotContain("<Green>", xml);
            Assert.DoesNotContain("<Blue>", xml);
            Assert.DoesNotContain("<Alpha>", xml);
            Assert.DoesNotContain("ColorOperation", xml);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ToAnimationChainList_FlipDiagonalAndColorChannels_PropagatedToRuntimeFrame()
    {
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream(FullFieldsAchx));
        var list = save.ToAnimationChainList(_ => null);
        var frame = list["Flash"]![0];

        Assert.True(frame.FlipDiagonal);
        Assert.Equal(200, frame.Red);
        Assert.Equal(100, frame.Green);
        Assert.Equal(50, frame.Blue);
        Assert.Equal(255, frame.Alpha);
        Assert.Equal(ColorOperation.Add, frame.ColorOperation);
    }

    // #793: an omitted channel on a later frame is "sticky" - it holds whatever an earlier frame
    // in the same chain last set, rather than resetting to null/none.
    [Fact]
    public void ToAnimationChainList_LaterFrameOmitsChannel_InheritsEarlierFramesValue()
    {
        const string achx = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>false</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Flash</Name>
                <Frame>
                  <TextureName>t.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                  <Red>200</Red>
                  <ColorOperation>Multiply</ColorOperation>
                </Frame>
                <Frame>
                  <TextureName>t.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                  <Green>100</Green>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream(achx));
        var list = save.ToAnimationChainList(_ => null);
        var secondFrame = list["Flash"]![1];

        Assert.Equal(200, secondFrame.Red);
        Assert.Equal(100, secondFrame.Green);
        Assert.Equal(ColorOperation.Multiply, secondFrame.ColorOperation);
    }

    [Fact]
    public void ToAnimationChainList_ChannelNeverAuthored_StaysNull()
    {
        var save = AnimationChainListSave.FromFile("f.achx", XmlStream("""
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave>
              <FileRelativeTextures>false</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Idle</Name>
                <Frame>
                  <TextureName>t.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate><RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate><BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """));
        var list = save.ToAnimationChainList(_ => null);
        var frame = list["Idle"]![0];

        Assert.Null(frame.Red);
        Assert.Null(frame.ColorOperation);
    }

    [Fact]
    public void ToAnimationChainList_FrameEvents_CopiedToRuntimeFrame()
    {
        var frameSave = new AnimationFrameSave { FrameLength = 0.1f };
        frameSave.Events.Add(new AnimationFrameEvent { Name = "Footstep", Data = "left" });
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Walk", Frames = { frameSave } });

        var frameEvent = Assert.Single(save.ToAnimationChainList(_ => null)["Walk"]![0].Events);

        Assert.Equal("Footstep", frameEvent.Name);
        Assert.Equal("left", frameEvent.Data);
        Assert.NotSame(frameSave.Events[0], frameEvent);
    }
}
