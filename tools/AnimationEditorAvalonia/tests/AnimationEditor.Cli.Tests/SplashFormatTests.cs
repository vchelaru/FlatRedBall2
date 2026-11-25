using System.Globalization;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class SplashFormatTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "sheet.png");

    public void Dispose() => _w.Dispose();

    private string Achx(params string[] frames) => _w.Write("t.achx",
        "<AnimationChainArraySave><CoordinateType>Pixel</CoordinateType><AnimationChain><Name>A</Name>"
        + string.Concat(frames.Select(f => $"<Frame><TextureName>sheet.png</TextureName><FrameLength>0.1</FrameLength><RightCoordinate>8</RightCoordinate><BottomCoordinate>8</BottomCoordinate>{f}</Frame>"))
        + "</AnimationChain></AnimationChainArraySave>");

    private static string Shape(string type, string name) =>
        $"<ShapeCollectionSave><{type}s><{type}>{name}</{type}></{type}s></ShapeCollectionSave>";

    [Theory]
    [InlineData("a.achj", "{\"version\":1,\"coordinateType\":\"Pixel\",\"sequences\":[{\"name\":\"Boss\",\"tracks\":[]}]}")]
    [InlineData("a.achj", "{\"coordinateType\":\"Pixel\",\"animationChains\":{\"Walk\":[]}}")]
    [InlineData("a.achj", "{\"coordinateType\":\"Pixel\",\"animationChains\":[{\"name\":\"Walk\",\"frames\":{\"0\":{}}}]}")]
    [InlineData("a.achj", "{\"coordinateType\":\"Pixel\",\"AnimationChains\":[{\"Name\":\"Walk\"}]}")]
    [InlineData("a.achx", "<tileset name=\"x\"><CoordinateType>Pixel</CoordinateType><image source=\"a.png\"/></tileset>")]
    public async Task Edit_FileWhoseContentIsNotRead_IsRefusedAndKept(string name, string text)
    {
        var path = _w.Write(name, text);

        var r = await _w.RunAsync("edit", path, "--op", "add-chain name=New");

        File.ReadAllText(path).ShouldBe(text);
        r.Code.ShouldBe(1, r.Out + r.Err);
    }

    [Theory]
    [InlineData("set-rect A/0/rect/H x=1234.5678", "1234.5678")]
    [InlineData("set-shape-common A/0/rect/H x=1234.5678", "1234.5678")]
    [InlineData("set-frame-offset A/0 x=1234.5678", "1234.5678")]
    [InlineData("set-frame-length A/0 length=0.016666667", "0.016666667")]
    public async Task Edit_NumberWithMoreThanSevenDigits_IsStoredAsGiven(string op, string number)
    {
        var path = Achx(Shape("AxisAlignedRectangleSave", "<Name>H</Name>"));

        var r = await _w.RunAsync("edit", path, "--op", op);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var stored = float.Parse(number, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
        File.ReadAllText(path).ShouldContain($">{stored}<");
        (await _w.RunAsync("edit", path, "--op", op)).Out.ShouldContain("No changes");
    }

    [Theory]
    [InlineData("set-rect A/0/rect/H", "X")]
    [InlineData("set-frame-offset A/0", "RelativeX")]
    public async Task Edit_RelativeToNumberWithMoreThanSevenDigits_KeepsItsDigits(string op, string element)
    {
        var path = Achx(Shape("AxisAlignedRectangleSave", "<Name>H</Name>"));
        (await _w.RunAsync("edit", path, "--op", $"{op} x=1234.5678")).Code.ShouldBe(0);

        var r = await _w.RunAsync("edit", path, "--op", $"{op} x=+2");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var stored = (1234.5678f + 2f).ToString("R", CultureInfo.InvariantCulture);
        File.ReadAllText(path).ShouldContain($"<{element}>{stored}</{element}>");
    }

    [Fact]
    public async Task Check_UnnamedShape_IsAnError()
    {
        var r = await _w.RunAsync("check", Achx(Shape("CircleSave", "<Name />")));

        r.Code.ShouldBe(1, r.Out);
        r.Out.ShouldContain("A circle in frame A/0 has no name.");
    }

    [Fact]
    public async Task Check_ShapeNameUsedForTwoKinds_IsAnError()
    {
        var r = await _w.RunAsync("check", Achx(Shape("CircleSave", "<Name>Hit</Name>"), Shape("AxisAlignedRectangleSave", "<Name>Hit</Name>")));

        r.Code.ShouldBe(1, r.Out);
        r.Out.ShouldContain("'Hit' is used for a circle (frame A/0) and a rect (frame A/1)");
    }

    [Fact]
    public async Task Check_ShapeNameUsedForOneKindInSeveralFrames_IsOk()
    {
        var r = await _w.RunAsync("check", Achx(Shape("CircleSave", "<Name>Hit</Name>"), Shape("CircleSave", "<Name>Hit</Name>")));

        r.Code.ShouldBe(0, r.Out);
    }

    [Fact]
    public async Task Edit_MakesAShapeNameTwoKinds_IsRefused()
    {
        var path = Achx(Shape("CircleSave", "<Name>Hit</Name>"), "");
        var before = File.ReadAllText(path);

        var r = await _w.RunAsync("edit", path, "--op", "add-shape A/1 type=rect name=Hit");

        r.Code.ShouldBe(1, r.Out + r.Err);
        File.ReadAllText(path).ShouldBe(before);
    }
}
