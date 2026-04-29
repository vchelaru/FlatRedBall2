using System.IO;
using System.Text;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class AdobeAnimateAtlasSaveTests
{
    private const string SampleXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TextureAtlas imagePath=""Eyeball_Idle.png"">
    <SubTexture name=""Eyeball_Idle0000"" x=""40"" y=""40"" width=""155"" height=""159"" pivotX=""77.1"" pivotY=""79.6""/>
    <SubTexture name=""Eyeball_Idle0002"" x=""40"" y=""40"" width=""155"" height=""159""/>
    <SubTexture name=""Eyeball_Idle0001"" x=""40"" y=""239"" width=""155"" height=""159""/>
    <SubTexture name=""Eyeball_Blink0000"" x=""235"" y=""40"" width=""155"" height=""159""/>
    <SubTexture name=""Eyeball_Blink0001"" x=""235"" y=""239"" width=""155"" height=""159""/>
</TextureAtlas>";

    private static AdobeAnimateAtlasSave Parse(string xml)
        => AdobeAnimateAtlasSave.FromFile("test.xml",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void Deserialize_ParsesImagePathAndSubTextures()
    {
        var save = Parse(SampleXml);

        save.ImagePath.ShouldBe("Eyeball_Idle.png");
        save.SubTextures.Count.ShouldBe(5);
        save.SubTextures[0].Name.ShouldBe("Eyeball_Idle0000");
        save.SubTextures[0].X.ShouldBe(40);
        save.SubTextures[0].Width.ShouldBe(155);
    }

    [Fact]
    public void Deserialize_PivotAttributes_ParsedWhenPresentNaNWhenAbsent()
    {
        var save = Parse(SampleXml);

        save.SubTextures[0].PivotX.ShouldBe(77.1f, tolerance: 0.01f);
        save.SubTextures[0].PivotY.ShouldBe(79.6f, tolerance: 0.01f);
        float.IsNaN(save.SubTextures[1].PivotX).ShouldBeTrue();
        float.IsNaN(save.SubTextures[1].PivotY).ShouldBeTrue();
    }

    [Fact]
    public void ToAnimationChainList_BottomCenterPivot_AnchorsAtFeet()
    {
        var save = Parse(PivotXml);
        var list = save.BuildList(null, frameRate: 30f);

        list[0][1].RelativeX.ShouldBe(0f, tolerance: 0.0001f);
        list[0][1].RelativeY.ShouldBe(50f, tolerance: 0.0001f);
    }

    [Fact]
    public void ToAnimationChainList_CenteredPivot_ProducesZeroOffsets()
    {
        var save = Parse(PivotXml);
        var list = save.BuildList(null, frameRate: 30f);

        list[0][0].RelativeX.ShouldBe(0f, tolerance: 0.0001f);
        list[0][0].RelativeY.ShouldBe(0f, tolerance: 0.0001f);
    }

    [Fact]
    public void ToAnimationChainList_FrameLengthIsOneOverFrameRate()
    {
        var save = Parse(SampleXml);
        var list = save.BuildList(null, frameRate: 24f);

        list[0][0].FrameLength.TotalSeconds.ShouldBe(1.0 / 24.0, tolerance: 0.0001);
    }

    [Fact]
    public void ToAnimationChainList_GroupsByNamePrefixAndSortsByFrameNumber()
    {
        var save = Parse(SampleXml);
        var list = save.BuildList(null, frameRate: 30f);

        list.Count.ShouldBe(2);
        list[0].Name.ShouldBe("Eyeball_Idle");
        list[1].Name.ShouldBe("Eyeball_Blink");

        list[0].Count.ShouldBe(3);
        list[0][0].SourceRectangle!.Value.Y.ShouldBe(40);   // 0000
        list[0][1].SourceRectangle!.Value.Y.ShouldBe(239);  // 0001
        list[0][2].SourceRectangle!.Value.Y.ShouldBe(40);   // 0002

        list[1].Count.ShouldBe(2);
    }

    [Fact]
    public void ToAnimationChainList_NoPivotAttributes_LeavesOffsetsAtZero()
    {
        var save = Parse(PivotXml);
        var list = save.BuildList(null, frameRate: 30f);

        list[0][3].RelativeX.ShouldBe(0f, tolerance: 0.0001f);
        list[0][3].RelativeY.ShouldBe(0f, tolerance: 0.0001f);
    }

    [Fact]
    public void ToAnimationChainList_TopLeftPivot_ShiftsSpriteRightAndDown()
    {
        var save = Parse(PivotXml);
        var list = save.BuildList(null, frameRate: 30f);

        list[0][2].RelativeX.ShouldBe(50f, tolerance: 0.0001f);
        list[0][2].RelativeY.ShouldBe(-50f, tolerance: 0.0001f);
    }

    private const string PivotXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<TextureAtlas imagePath=""char.png"">
    <SubTexture name=""Char0000"" x=""0"" y=""0"" width=""100"" height=""100"" pivotX=""50"" pivotY=""50""/>
    <SubTexture name=""Char0001"" x=""0"" y=""0"" width=""100"" height=""100"" pivotX=""50"" pivotY=""100""/>
    <SubTexture name=""Char0002"" x=""0"" y=""0"" width=""100"" height=""100"" pivotX=""0""  pivotY=""0""/>
    <SubTexture name=""Char0003"" x=""0"" y=""0"" width=""100"" height=""100""/>
</TextureAtlas>";
}
