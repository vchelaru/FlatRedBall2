using System;
using System.IO;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>FRB1 leaves a null field out and writes fields FRB2 does not model; saving an unedited file must not change that.</summary>
public class Frb1FieldRoundTripTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.achx");

    public void Dispose() => File.Delete(_path);

    private const string Shape = """
              <ShapeCollectionSave>
                <AxisAlignedRectangleSaves>
                  <AxisAlignedRectangleSave>
                    <X>0</X>
                    <Y>0</Y>
                    <Z>0</Z>
                    <ScaleX>8</ScaleX>
                    <ScaleY>8</ScaleY>
                    <Alpha>1</Alpha>
                    <Red>1</Red>
                    <Green>1</Green>
                    <Blue>1</Blue>
                  </AxisAlignedRectangleSave>
                </AxisAlignedRectangleSaves>
                <AxisAlignedCubeSaves />
                <PolygonSaves />
                <CircleSaves />
                <SphereSaves />
              </ShapeCollectionSave>

        """;

    [Theory]
    [InlineData("    <ParentFile>walk.gif</ParentFile>\n", "      <TextureName>a.png</TextureName>\n", "")]
    [InlineData("", "      <TextureName />\n", "")]
    [InlineData("    <ParentFile />\n", "      <TextureName></TextureName>\n", "")]
    [InlineData("    <ColorKey>0</ColorKey>\n", "      <TextureName>a.png</TextureName>\n", "")]
    [InlineData("    <ColorKey>4278190080</ColorKey>\n    <ParentFile>walk.gif</ParentFile>\n", "", "")]
    [InlineData("", "", "")]
    [InlineData("", "      <TextureName>a.png</TextureName>\n", Shape)]
    public void Save_UneditedFrb1File_IsByteIdentical(string chainFields, string texture, string shapes)
    {
        string text = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <FileRelativeTextures>true</FileRelativeTextures>
              <TimeMeasurementUnit>Undefined</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Walk</Name>
            {chainFields}    <Frame>
            {texture}      <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate>
                  <RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate>
                  <BottomCoordinate>1</BottomCoordinate>
            {shapes}    </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """.Replace("\r\n", "\n");
        File.WriteAllText(_path, text);

        AnimationChainListSave.FromFile(_path).Save(_path);

        File.ReadAllText(_path).ShouldBe(text);
    }

    // Every element FRB1 writes that FRB2 does not model, and empty text in each of the other objects.
    [Fact]
    public void Save_UneditedFrb1FileWithUnmodeledFields_IsByteIdentical()
    {
        string text = """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <FileRelativeTextures>true</FileRelativeTextures>
              <TimeMeasurementUnit>Undefined</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Frame>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate>
                  <RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate>
                  <BottomCoordinate>1</BottomCoordinate>
                  <OffsetInterpolation />
                  <ShapeCollectionSave>
                    <AxisAlignedRectangleSaves>
                      <AxisAlignedRectangleSave>
                        <X>0</X>
                        <Y>0</Y>
                        <Z>0</Z>
                        <ScaleX>8</ScaleX>
                        <ScaleY>8</ScaleY>
                        <Name />
                        <Parent>Body</Parent>
                        <Alpha>1</Alpha>
                        <Red>1</Red>
                        <Green>1</Green>
                        <Blue>1</Blue>
                        <SizeEasing />
                      </AxisAlignedRectangleSave>
                    </AxisAlignedRectangleSaves>
                    <AxisAlignedCubeSaves>
                      <AxisAlignedCubeSave>
                        <ScaleX>1</ScaleX>
                      </AxisAlignedCubeSave>
                    </AxisAlignedCubeSaves>
                    <PolygonSaves>
                      <PolygonSave>
                        <X>0</X>
                        <Y>0</Y>
                        <Z>0</Z>
                        <RotationZ>0.5</RotationZ>
                        <Points>
                          <Point>
                            <X>1</X>
                            <Y>2</Y>
                          </Point>
                        </Points>
                        <Name>Hull</Name>
                        <Alpha>1</Alpha>
                        <Red>1</Red>
                        <Green>1</Green>
                        <Blue>1</Blue>
                      </PolygonSave>
                    </PolygonSaves>
                    <CircleSaves />
                    <SphereSaves>
                      <SphereSave>
                        <Radius>1</Radius>
                      </SphereSave>
                    </SphereSaves>
                  </ShapeCollectionSave>
                  <Events>
                    <Event>
                      <Name>Step</Name>
                      <Data />
                    </Event>
                  </Events>
                </Frame>
              </AnimationChain>
              <FileName>C:/Game/Content/Walk.achx</FileName>
              <ProjectFile />
            </AnimationChainArraySave>
            """.Replace("\r\n", "\n");
        File.WriteAllText(_path, text);

        AnimationChainListSave.FromFile(_path).Save(_path);

        File.ReadAllText(_path).ShouldBe(text);
    }

    [Fact]
    public void ToXmlString_AbsentNameGivenText_WritesIt()
    {
        var save = AnimationChainListSave.FromString("<AnimationChainArraySave><AnimationChain /></AnimationChainArraySave>");

        save.AnimationChains[0].Name = "Walk";

        save.ToXmlString().ShouldContain("<Name>Walk</Name>");
    }

    // RotationZ is only carried, so it is never parsed, and the name stays on the side of the points it was on.
    [Theory]
    [InlineData("<RotationZ />", "", "<Name>Hull</Name>")]
    [InlineData("<RotationZ>INF</RotationZ>", "", "<Name>Hull</Name>")]
    [InlineData("<RotationZ>0,5</RotationZ>", "<Name>Hull</Name>", "")]
    [InlineData("", "", "<Name>Hull</Name>")]
    public void ToXmlString_PolygonRotationAndName_StayAsRead(string rotation, string nameBefore, string nameAfter)
    {
        string polygon = $"<PolygonSave>{nameBefore}<X>0</X><Y>0</Y><Z>0</Z>{rotation}<Points />{nameAfter}";

        string saved = AnimationChainListSave.FromString(
            $"<AnimationChainArraySave><AnimationChain><Frame><ShapeCollectionSave><PolygonSaves>{polygon}</PolygonSave></PolygonSaves></ShapeCollectionSave></Frame></AnimationChain></AnimationChainArraySave>")
            .ToXmlString();

        System.Text.RegularExpressions.Regex.Replace(saved, @">\s+<", "><").ShouldContain(polygon + "<Alpha>");
    }

    // The nested dialect is rewritten into FRB1's on save, so its empty text has no form to keep.
    [Theory]
    [InlineData("")]
    [InlineData("<Name /><Name>b</Name>")]
    public void ToXmlString_NestedShapeWithEmptyName_WritesAPairOfTags(string name)
    {
        string saved = AnimationChainListSave.FromString(
            $"<AnimationChainArraySave><AnimationChain><Name>Walk</Name><Frame><ShapeCollectionSave><Shapes><AxisAlignedRectangleSave>{name}<X>1</X></AxisAlignedRectangleSave></Shapes></ShapeCollectionSave></Frame></AnimationChain></AnimationChainArraySave>")
            .ToXmlString();

        saved.ShouldContain("<Name></Name>");
    }

    [Theory]
    [InlineData("0,5")]
    [InlineData("1,25")]
    public void FromString_CommaDecimal_IsRefused(string length) =>
        Should.Throw<InvalidDataException>(() => AnimationChainListSave.FromString(
            $"<AnimationChainArraySave><AnimationChain><Name>A</Name><Frame><FrameLength>{length}</FrameLength></Frame></AnimationChain></AnimationChainArraySave>"))
            .Message.ShouldContain($"FrameLength '{length}'");
}
