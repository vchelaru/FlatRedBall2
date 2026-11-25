using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class ConvertCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "sheet.png");

    public void Dispose() => _w.Dispose();

    [Fact]
    public async Task AchxToAchj_CreatesAchjAndKeepsAchx()
    {
        var before = _w.Bytes("hero.achx");

        var r = await _w.RunAsync("convert", _w.PathOf("hero.achx"), "--to", "achj");

        r.Code.ShouldBe(0, r.Out + r.Err);
        File.Exists(_w.PathOf("hero.achj")).ShouldBeTrue();
        _w.Bytes("hero.achx").ShouldBe(before);
        var check = await _w.RunAsync("check", _w.PathOf("hero.achj"));
        check.Code.ShouldBe(0, check.Out);
    }

    [Fact]
    public async Task RoundTrip_AchxToAchjToAchx_KeepsTheDescription()
    {
        var original = (await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json")).Out;
        (await _w.RunAsync("convert", _w.PathOf("hero.achx"), "--to", "achj")).Code.ShouldBe(0);
        File.Move(_w.PathOf("hero.achx"), _w.PathOf("original.achx.bak"));

        var r = await _w.RunAsync("convert", _w.PathOf("hero.achj"), "--to", "achx");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var again = (await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json")).Out;
        JsonSerializer.Serialize(JsonDocument.Parse(again).RootElement.GetProperty("chains"))
            .ShouldBe(JsonSerializer.Serialize(JsonDocument.Parse(original).RootElement.GetProperty("chains")));
    }

    [Fact]
    public async Task TargetExists_ExitsOneAndLeavesIt()
    {
        File.WriteAllText(_w.PathOf("hero.achj"), "keep me");

        var r = await _w.RunAsync("convert", _w.PathOf("hero.achx"), "--to", "achj", "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        File.ReadAllText(_w.PathOf("hero.achj")).ShouldBe("keep me");
    }

    [Fact]
    public async Task Json_ReportsTheNewFile()
    {
        var r = await _w.RunAsync("convert", _w.PathOf("hero.achx"), "--to", "achj", "--json");

        r.Code.ShouldBe(0, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("changed")[0].GetString().ShouldBe(_w.PathOf("hero.achj"));
    }

    // A file the other format cannot hold exactly, with what the refusal names.
    [Theory]
    [InlineData("<ColorKey>7</ColorKey>", "", "<ColorKey> (animation 'Walk')")]
    [InlineData("", "<ShapeCollectionSave><AxisAlignedCubeSaves><AxisAlignedCubeSave><X>1</X></AxisAlignedCubeSave></AxisAlignedCubeSaves></ShapeCollectionSave>", "<AxisAlignedCubeSaves> (animation 'Walk', frame 1)")]
    [InlineData("", "<ShapeCollectionSave><SphereSaves><SphereSave><Radius>2</Radius></SphereSave></SphereSaves></ShapeCollectionSave>", "<SphereSaves> (animation 'Walk', frame 1)")]
    [InlineData("", "<ShapeCollectionSave><CircleSaves><CircleSave><Radius>2</Radius><Parent>Body</Parent></CircleSave></CircleSaves></ShapeCollectionSave>", "<Parent> (animation 'Walk', frame 1)")]
    [InlineData("", "<Custom>5</Custom>", "<Custom>")]
    [InlineData("", "<FlipVertical by=\"me\">false</FlipVertical>", "the attribute by")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius><Custom>5</Custom></CircleSave></Shapes></ShapeCollectionSave>", "<Custom>")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius></CircleSave><SphereSave><Radius>2</Radius></SphereSave></Shapes></ShapeCollectionSave>", "<SphereSave>")]
    public async Task AchxToAchj_WouldLoseSomething_IsRefusedNamingIt(string inChain, string inFrame, string names)
    {
        _w.Write("a.achx", $"<AnimationChainArraySave><AnimationChain><Name>Walk</Name>{inChain}<Frame><TextureName>sheet.png</TextureName>{inFrame}<FrameLength>1</FrameLength></Frame></AnimationChain></AnimationChainArraySave>");

        await ShouldBeRefused("a.achx", "achj", names);
    }

    [Theory]
    [InlineData("\"custom\":{\"a\":1},", "Walk", "", "custom")]
    [InlineData("", "Walk", "\"custom\":[1],", "animationChains[0].frames[0].custom")]
    [InlineData("", "Walk", "\"custom\":false,", "animationChains[0].frames[0].custom")]
    [InlineData("", "Walk", "\"shapes\":{\"circles\":[{\"radius\":2,\"custom\":5}]},", "animationChains[0].frames[0].shapes.circles[0].custom")]
    [InlineData("", "  ", "", "animationChains[0].name")]
    [InlineData("", "a\\rb", "", "animationChains[0].name")]
    public async Task AchjToAchx_WouldLoseSomething_IsRefusedNamingIt(string inList, string chain, string inFrame, string names)
    {
        _w.Write("a.achj", $$"""{ {{inList}} "animationChains":[{"name":"{{chain}}","frames":[{ {{inFrame}} "frameLength":1}]}]}""");

        await ShouldBeRefused("a.achj", "achx", names);
    }

    private async Task ShouldBeRefused(string file, string to, string names)
    {
        var before = Directory.GetFiles(_w.Dir);

        var r = await _w.RunAsync("convert", _w.PathOf(file), "--to", to, "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        var error = doc.RootElement.GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("WouldLoseData");
        error.GetProperty("message").GetString().ShouldBe($"{file} is not converted: {names} would not be kept. Remove or change it first.");
        Directory.GetFiles(_w.Dir).ShouldBe(before);
    }

    // Values left out of what is written, because they are the default, are kept.
    [Fact]
    public async Task AchjToAchx_DefaultsAndEmptyUnknownProperties_AreNoLoss()
    {
        _w.Write("a.achj", """
            {"custom":null,"other":{},"fileRelativeTextures":true,"projectFile":null,"animationChains":[{"name":"Walk","locked":false,"loop":true,"pingPong":false,"reverse":false,"frames":[
              {"frameLength":1,"flipHorizontal":false,"flipVertical":false,"flipDiagonal":false,"relativeX":0,"relativeY":0,"red":null,"skipOffsetKeyframe":false,"skipColorKeyframe":false,"offsetEasing":"","colorOperation":null,
               "events":[null,{"name":"Hit","data":null}],
               "shapes":{"circles":[{"name":"A","skipOffsetKeyframe":false,"skipSizeKeyframe":false}],"polygons":[{"name":"B","skipPointsKeyframe":false,"points":[{"x":1,"y":2}]}]}},
              {"frameLength":1,"events":[]}]}]}
            """);

        var r = await _w.RunAsync("convert", _w.PathOf("a.achj"), "--to", "achx");

        r.Code.ShouldBe(0, r.Out + r.Err);
    }

    // AnimationChain.MonoGame's shapes, which are written back in other elements.
    [Fact]
    public async Task AchxToAchj_NestedShapes_AreNoLoss()
    {
        _w.Write("a.achx", "<AnimationChainArraySave><AnimationChain><Name>Walk</Name><Frame><TextureName>sheet.png</TextureName><FrameLength>1</FrameLength><ShapeCollectionSave><Shapes>"
            + "<CircleSave><Name>A</Name><X>0</X><Radius>2</Radius></CircleSave><PolygonSave><Name>B</Name><Points><Vector2Save><X>1</X><Y>2</Y></Vector2Save></Points></PolygonSave>"
            + "</Shapes></ShapeCollectionSave></Frame></AnimationChain></AnimationChainArraySave>");

        var r = await _w.RunAsync("convert", _w.PathOf("a.achx"), "--to", "achj");

        r.Code.ShouldBe(0, r.Out + r.Err);
    }

    [Fact]
    public async Task AchxToAchj_Comments_AreAWarning()
    {
        _w.Write("a.achx", "<!-- note --><AnimationChainArraySave><AnimationChain><Name>Walk</Name></AnimationChain></AnimationChainArraySave>");

        var r = await _w.RunAsync("convert", _w.PathOf("a.achx"), "--to", "achj", "--json");

        r.Code.ShouldBe(0, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("warnings")[0].GetString().ShouldBe("a.achx: comments are not kept.");
        File.Exists(_w.PathOf("a.achj")).ShouldBeTrue();
    }

    [Theory]
    [InlineData("convert")]
    [InlineData("convert", "hero.achx")]
    [InlineData("convert", "hero.achx", "--to", "png")]
    [InlineData("convert", "hero.achx", "--to")]
    public async Task BadCommandLine_IsAUsageError(params string[] args)
    {
        var resolved = args.Select(a => a == "hero.achx" ? _w.PathOf(a) : a).ToArray();

        var r = await _w.RunAsync(resolved);

        r.Code.ShouldBe(2, r.Out + r.Err);
    }

    [Fact]
    public async Task SameFormat_ExitsOne()
    {
        var r = await _w.RunAsync("convert", _w.PathOf("hero.achx"), "--to", "achx");

        r.Code.ShouldBe(1, r.Out + r.Err);
    }

    [Fact]
    public async Task Usage_ListsConvert()
    {
        var r = await _w.RunAsync("--help");

        r.Out.ShouldContain("convert");
    }
}
