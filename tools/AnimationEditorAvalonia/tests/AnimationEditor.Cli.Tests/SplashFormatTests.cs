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
}
