using System.IO;
using System.Text;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

/// <summary>
/// A bad enum value in an .achx/.achj must fail with a message naming where it is and what is
/// valid, not the bare "Requested value 'X' was not found." from Enum.Parse (#1548).
/// </summary>
public class AnimationChainListSaveEnumErrorTests
{
    private static AnimationChainListSave LoadXml(string xml)
        => AnimationChainListSave.FromFile("test.achx", _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    private static AnimationChainListSave LoadJson(string json)
        => AnimationChainListSave.FromFile("test.achj", _ => new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void FromFile_Achj_BadColorOperation_NamesAnimationFrameAndValidValues()
    {
        var json = """
            {
              "animationChains": [
                { "name": "7_Color_PingPong", "frames": [
                  { "colorOperation": "Add" },
                  { "colorOperation": "Modulate" }
                ] }
              ]
            }
            """;

        var ex = Should.Throw<InvalidDataException>(() => LoadJson(json));

        ex.Message.ShouldBe("Animation '7_Color_PingPong', frame 2: ColorOperation 'Modulate' isn't valid (expected Multiply or Add).");
    }

    [Fact]
    public void FromFile_Achj_BadCoordinateType_NamesValidValues()
    {
        var ex = Should.Throw<InvalidDataException>(() => LoadJson("""{ "coordinateType": "Texel" }"""));

        ex.Message.ShouldBe("CoordinateType 'Texel' isn't valid (expected UV or Pixel).");
    }

    [Fact]
    public void FromFile_Achj_BadTimeMeasurementUnit_NamesValidValues()
    {
        var ex = Should.Throw<InvalidDataException>(() => LoadJson("""{ "timeMeasurementUnit": "Minute" }"""));

        ex.Message.ShouldBe("TimeMeasurementUnit 'Minute' isn't valid (expected Undefined, Second or Millisecond).");
    }

    [Fact]
    public void FromFile_Achx_BadColorOperation_NamesAnimationFrameLineAndValidValues()
    {
        var xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<AnimationChainArraySave>\n" +
            "  <AnimationChain>\n" +
            "    <Name>7_Color_PingPong</Name>\n" +
            "    <Frame>\n" +
            "      <ColorOperation>Modulate</ColorOperation>\n" +
            "    </Frame>\n" +
            "  </AnimationChain>\n" +
            "</AnimationChainArraySave>\n";

        var ex = Should.Throw<InvalidDataException>(() => LoadXml(xml));

        ex.Message.ShouldBe("Line 6: Animation '7_Color_PingPong', frame 1: ColorOperation 'Modulate' isn't valid (expected Multiply or Add).");
    }

    [Fact]
    public void FromFile_Achx_BadCoordinateType_NamesLineAndValidValues()
    {
        var xml =
            "<AnimationChainArraySave>\n" +
            "  <CoordinateType>Texel</CoordinateType>\n" +
            "</AnimationChainArraySave>";

        var ex = Should.Throw<InvalidDataException>(() => LoadXml(xml));

        ex.Message.ShouldBe("Line 2: CoordinateType 'Texel' isn't valid (expected UV or Pixel).");
    }

    [Fact]
    public void FromFile_Achx_BadTimeMeasurementUnit_NamesLineAndValidValues()
    {
        var xml =
            "<AnimationChainArraySave>\n" +
            "  <TimeMeasurementUnit>Minute</TimeMeasurementUnit>\n" +
            "</AnimationChainArraySave>";

        var ex = Should.Throw<InvalidDataException>(() => LoadXml(xml));

        ex.Message.ShouldBe("Line 2: TimeMeasurementUnit 'Minute' isn't valid (expected Undefined, Second or Millisecond).");
    }

    [Fact]
    public void FromString_Achx_BadColorOperation_IncludesLineNumber()
    {
        var xml =
            "<AnimationChainArraySave>\n" +
            "  <AnimationChain>\n" +
            "    <Name>Walk</Name>\n" +
            "    <Frame><ColorOperation>Modulate</ColorOperation></Frame>\n" +
            "  </AnimationChain>\n" +
            "</AnimationChainArraySave>";

        var ex = Should.Throw<InvalidDataException>(() => AnimationChainListSave.FromString(xml));

        ex.Message.ShouldStartWith("Line 4: Animation 'Walk', frame 1: ");
    }
}
