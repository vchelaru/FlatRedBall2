using System.IO;
using System.Xml.Serialization;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class AnimationChainListSaveTests
{
    // Minimal helper: round-trip serialize then deserialize to simulate FromFile without disk I/O
    private static AnimationChainListSave RoundTrip(AnimationChainListSave save)
    {
        var serializer = new XmlSerializer(typeof(AnimationChainListSave));
        using var ms = new MemoryStream();
        serializer.Serialize(ms, save);
        ms.Position = 0;
        return (AnimationChainListSave)serializer.Deserialize(ms)!;
    }

    [Fact]
    public void RoundTrip_SingleChainWithTwoFrames_PreservesNamesAndFrameCount()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "walk1.png", FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "walk2.png", FrameLength = 0.1f });
        save.AnimationChains.Add(chain);

        var result = RoundTrip(save);

        result.AnimationChains.Count.ShouldBe(1);
        result.AnimationChains[0].Name.ShouldBe("Walk");
        result.AnimationChains[0].Frames.Count.ShouldBe(2);
        result.AnimationChains[0].Frames[0].TextureName.ShouldBe("walk1.png");
        result.AnimationChains[0].Frames[1].FrameLength.ShouldBe(0.1f, tolerance: 0.0001f);
    }

    [Fact]
    public void RoundTrip_FlipHorizontalFalse_NotSerializedThenDeserializesToFalse()
    {
        // FlipHorizontal=false should be omitted from XML (ShouldSerialize returns false).
        // After round-trip the default value (false) is still correct.
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "idle.png", FlipHorizontal = false });
        save.AnimationChains.Add(chain);

        var serializer = new XmlSerializer(typeof(AnimationChainListSave));
        using var ms = new MemoryStream();
        serializer.Serialize(ms, save);
        string xml = System.Text.Encoding.UTF8.GetString(ms.ToArray());

        // The element must not appear in the XML when false
        xml.ShouldNotContain("FlipHorizontal");

        ms.Position = 0;
        var result = (AnimationChainListSave)serializer.Deserialize(ms)!;
        result.AnimationChains[0].Frames[0].FlipHorizontal.ShouldBeFalse();
    }

    [Fact]
    public void RoundTrip_TimeMeasurementUnitMillisecond_RoundTripsCorrectly()
    {
        var save = new AnimationChainListSave
        {
            TimeMeasurementUnit = TimeMeasurementUnit.Millisecond
        };

        var result = RoundTrip(save);

        result.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
    }
}
