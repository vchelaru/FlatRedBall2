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

    // Each thing the .achj does not keep, with what its warning names.
    [Theory]
    [InlineData("<ColorKey>7</ColorKey>", "", "<ColorKey> in animation 'Walk'")]
    [InlineData("<ColorKey>7</ColorKey><ColorKey>8</ColorKey>", "", "<ColorKey> in animation 'Walk'")]
    [InlineData("<ParentFile>a.gif</ParentFile>", "", "<ParentFile> in animation 'Walk'")]
    [InlineData("", "<ShapeCollectionSave><AxisAlignedCubeSaves><AxisAlignedCubeSave><X>1</X></AxisAlignedCubeSave></AxisAlignedCubeSaves></ShapeCollectionSave>", "<AxisAlignedCubeSaves> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><SphereSaves><SphereSave><Radius>2</Radius></SphereSave></SphereSaves></ShapeCollectionSave>", "<SphereSaves> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><CircleSaves><CircleSave><Radius>2</Radius><Parent>Body</Parent></CircleSave></CircleSaves></ShapeCollectionSave>", "<Parent> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><AxisAlignedRectangleSaves><AxisAlignedRectangleSave><Parent>Body</Parent></AxisAlignedRectangleSave></AxisAlignedRectangleSaves></ShapeCollectionSave>", "<Parent> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><PolygonSaves><PolygonSave><RotationZ>2</RotationZ></PolygonSave></PolygonSaves></ShapeCollectionSave>", "<RotationZ> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius><Parent>Body</Parent></CircleSave></Shapes></ShapeCollectionSave>", "<Parent> in animation 'Walk', frame 1")]
    [InlineData("<Custom><Part>5</Part></Custom>", "<RelativeX>4</RelativeX>", "<Custom> in animation 'Walk'")]
    [InlineData("", "<Custom>5</Custom>", "<Custom> in animation 'Walk', frame 1")]
    [InlineData("", "<FlipVertical by=\"me\">false</FlipVertical>", "the attribute by in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius><Custom>5</Custom></CircleSave></Shapes></ShapeCollectionSave>", "<Custom> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius></CircleSave><SphereSave><Radius>2</Radius></SphereSave></Shapes></ShapeCollectionSave>", "<SphereSave> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes><PolygonSave><Points><Vector2Save><X>1</X></Vector2Save></Points><Points><Vector2Save><X>5</X></Vector2Save></Points></PolygonSave></Shapes></ShapeCollectionSave>", "<Points> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius><Radius>9</Radius></CircleSave></Shapes></ShapeCollectionSave>", "<Radius> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes>hello<CircleSave><Radius>2</Radius></CircleSave></Shapes></ShapeCollectionSave>", "the text in <Shapes> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius></CircleSave></Shapes><Shapes><CircleSave><Radius>2</Radius></CircleSave></Shapes></ShapeCollectionSave>", "<Shapes> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes><CircleSave><Radius>2</Radius></CircleSave></Shapes><CircleSaves><CircleSave><Radius>9</Radius></CircleSave></CircleSaves></ShapeCollectionSave>", "<CircleSaves> in animation 'Walk', frame 1")]
    [InlineData("", "<ShapeCollectionSave><Shapes>hello</Shapes></ShapeCollectionSave>", "<Shapes> in animation 'Walk', frame 1")]
    [InlineData("", "<FlipVertical>false</FlipVertical><FlipVertical>true</FlipVertical>", "<FlipVertical> in animation 'Walk', frame 1")]
    public async Task AchxToAchj_WhatIsNotKept_IsAWarningNamingIt(string inChain, string inFrame, string names)
    {
        _w.Write("a.achx", $"<AnimationChainArraySave><AnimationChain><Name>Walk</Name>{inChain}<Frame><TextureName>sheet.png</TextureName>{inFrame}<FrameLength>1</FrameLength></Frame></AnimationChain></AnimationChainArraySave>");

        (await ConvertedWarningsAsync("a.achx", "achj")).ShouldBe([$"a.achx: {names} is not kept."]);
    }

    // Not only the first, and equal ones together.
    [Fact]
    public async Task AchxToAchj_SeveralThingsNotKept_AreAllWarnings()
    {
        string Chain(string name, string inFrames) =>
            $"<AnimationChain><Name>{name}</Name><ColorKey>0</ColorKey><Frame><FrameLength>1</FrameLength>{inFrames}</Frame><Frame><FrameLength>2</FrameLength>{inFrames}</Frame></AnimationChain>";
        _w.Write("a.achx", "<!-- note --><AnimationChainArraySave>" + Chain("Idle", "") + Chain("Walk", "<Custom>5</Custom>") + Chain("Run", "<Custom>5</Custom><Other a=\"1\" />")
            + Chain("Jump", "") + "<FileName>C:/old.achx</FileName><Custom>1</Custom><Custom>2</Custom></AnimationChainArraySave>");

        (await ConvertedWarningsAsync("a.achx", "achj")).ShouldBe(
        [
            "a.achx: <ColorKey> in 4 animations ('Idle', 'Walk', 'Run' and 1 more) is not kept.",
            "a.achx: <Custom> in 4 frames of 2 animations ('Walk' and 'Run') is not kept.",
            "a.achx: <Other> in 2 frames of animation 'Run' is not kept.",
            "a.achx: <FileName> is not kept.",
            "a.achx: <Custom> is not kept.",
            "a.achx: comments are not kept.",
        ], ignoreOrder: true);
    }

    // Everything the AnimationEditor reads is there, whatever else is not.
    [Fact]
    public async Task AchxToAchj_WithDrops_KeepsTheAnimations()
    {
        string content = File.ReadAllText(_w.PathOf("hero.achx"));
        _w.Write("hero.achx", content.Replace("</Name>", "</Name><ColorKey>0</ColorKey>"));
        var described = (await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json")).Out;

        (await ConvertedWarningsAsync("hero.achx", "achj")).ShouldNotBeEmpty();

        JsonSerializer.Serialize(JsonDocument.Parse((await _w.RunAsync("describe", _w.PathOf("hero.achj"), "--json")).Out).RootElement.GetProperty("chains"))
            .ShouldBe(JsonSerializer.Serialize(JsonDocument.Parse(described).RootElement.GetProperty("chains")));
    }

    [Theory]
    [InlineData("\"custom\":{\"a\":1},", "", "\"custom\"")]
    [InlineData("", "\"custom\":[1],", "\"custom\" in animation 'Walk', frame 1")]
    [InlineData("", "\"custom\":false,", "\"custom\" in animation 'Walk', frame 1")]
    [InlineData("", "\"shapes\":{\"circles\":[{\"radius\":2,\"custom\":5}]},", "\"custom\" in animation 'Walk', frame 1")]
    public async Task AchjToAchx_WhatIsNotKept_IsAWarningNamingIt(string inList, string inFrame, string names)
    {
        _w.Write("a.achj", $$"""{ {{inList}} "animationChains":[{"name":"Walk","frames":[{ {{inFrame}} "frameLength":1}]}]}""");

        (await ConvertedWarningsAsync("a.achj", "achx")).ShouldBe([$"a.achj: {names} is not kept."]);
    }

    [Fact]
    public async Task AchjToAchx_SeveralThingsNotKept_AreAllWarnings()
    {
        _w.Write("a.achj", """{"tool":1,"animationChains":[{"name":"Idle","tint":1,"frames":[]},{"name":"Walk","tint":2,"frames":[{"frameLength":1},{"frameLength":1,"tint":3}]}]}""");

        (await ConvertedWarningsAsync("a.achj", "achx")).ShouldBe(
        [
            "a.achj: \"tool\" is not kept.",
            "a.achj: \"tint\" in 2 animations ('Idle' and 'Walk') is not kept.",
            "a.achj: \"tint\" in animation 'Walk', frame 2 is not kept.",
        ]);
    }

    private async Task<string[]> ConvertedWarningsAsync(string file, string to)
    {
        var before = _w.Bytes(file);

        var r = await _w.RunAsync("convert", _w.PathOf(file), "--to", to, "--json");

        r.Code.ShouldBe(0, r.Out + r.Err);
        File.Exists(Path.ChangeExtension(_w.PathOf(file), to)).ShouldBeTrue();
        _w.Bytes(file).ShouldBe(before);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        return doc.RootElement.GetProperty("warnings").EnumerateArray().Select(warning => warning.GetString()!).ToArray();
    }

    // What the AnimationEditor reads must come out the same: an .achx does not keep this text.
    [Theory]
    [InlineData("  ")]
    [InlineData("a\\rb")]
    public async Task AchjToAchx_AnimationDataWouldComeOutDifferent_IsRefused(string chain)
    {
        _w.Write("a.achj", $$"""{"custom":1,"animationChains":[{"name":"{{chain}}","frames":[]}]}""");
        var before = Directory.GetFiles(_w.Dir);

        var r = await _w.RunAsync("convert", _w.PathOf("a.achj"), "--to", "achx", "--json");

        r.Code.ShouldBe(1, r.Out + r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        var error = doc.RootElement.GetProperty("errors")[0];
        error.GetProperty("code").GetString().ShouldBe("WouldLoseData");
        error.GetProperty("message").GetString().ShouldBe("a.achj is not converted: animationChains[0].name would not be kept. Remove or change it first.");
        Directory.GetFiles(_w.Dir).ShouldBe(before);
    }

    // FRB1 writes a polygon's name after its points.
    [Fact]
    public async Task AchxToAchj_OrderOfElements_IsNoLoss()
    {
        _w.Write("a.achx", "<AnimationChainArraySave><AnimationChain><Name>Walk</Name><Frame><FrameLength>1</FrameLength><ShapeCollectionSave><PolygonSaves>"
            + "<PolygonSave><Points><Point><X>1</X><Y>2</Y></Point></Points><Name>B</Name></PolygonSave></PolygonSaves></ShapeCollectionSave></Frame></AnimationChain></AnimationChainArraySave>");

        (await ConvertedWarningsAsync("a.achx", "achj")).ShouldBeEmpty();
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
