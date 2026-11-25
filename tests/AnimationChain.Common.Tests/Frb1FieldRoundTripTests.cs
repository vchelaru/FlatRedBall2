using System;
using System.IO;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>FRB1 leaves a null field out and writes a chain's ParentFile; saving an unedited file must not change that.</summary>
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
    [InlineData("", "", "")]
    [InlineData("", "      <TextureName>a.png</TextureName>\n", Shape)]
    public void Save_UneditedFrb1File_IsByteIdentical(string parentFile, string texture, string shapes)
    {
        string text = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <FileRelativeTextures>true</FileRelativeTextures>
              <TimeMeasurementUnit>Undefined</TimeMeasurementUnit>
              <CoordinateType>UV</CoordinateType>
              <AnimationChain>
                <Name>Walk</Name>
            {parentFile}    <Frame>
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
}
