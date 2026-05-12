using System.IO;
using System.Text;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class AnimationChainListSaveTests
{
    private static AnimationChainListSave Parse(string xml)
        => AnimationChainListSave.FromFile("test.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void FromFile_SingleChainWithTwoFrames_PreservesNamesAndFrameCount()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Walk</Name>" +
            "    <Frame><TextureName>walk1.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "    <Frame><TextureName>walk2.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var result = Parse(xml);

        result.AnimationChains.Count.ShouldBe(1);
        result.AnimationChains[0].Name.ShouldBe("Walk");
        result.AnimationChains[0].Frames.Count.ShouldBe(2);
        result.AnimationChains[0].Frames[0].TextureName.ShouldBe("walk1.png");
        result.AnimationChains[0].Frames[1].FrameLength.ShouldBe(0.1f, tolerance: 0.0001f);
    }

    [Fact]
    public void FromFile_FlipHorizontalOmitted_DefaultsToFalse()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Idle</Name>" +
            "    <Frame><TextureName>idle.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var result = Parse(xml);

        result.AnimationChains[0].Frames[0].FlipHorizontal.ShouldBeFalse();
    }

    [Fact]
    public void FromFile_TimeMeasurementUnitMillisecond_ParsedCorrectly()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <TimeMeasurementUnit>Millisecond</TimeMeasurementUnit>" +
            "</AnimationChainArraySave>";

        var result = Parse(xml);

        result.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
    }
}
