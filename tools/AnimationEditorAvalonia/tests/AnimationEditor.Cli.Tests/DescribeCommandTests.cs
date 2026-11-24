using System.Text.Json;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class DescribeCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task Text_ListsChainsFramesAndShapes(string file)
    {
        var r = await _w.RunAsync("describe", _w.PathOf(file));

        r.Code.ShouldBe(0);
        r.Out.ShouldContain("Walk");
        r.Out.ShouldContain("Idle");
        r.Out.ShouldContain("sheet.png");
        r.Out.ShouldContain("Hitbox");
        r.Out.ShouldContain("rect");
        r.Out.ShouldContain("circle");
        r.Out.ShouldContain("Foot");
    }

    [Fact]
    public async Task Json_HasChainsFramesByIndexAndShapes()
    {
        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json");

        r.Code.ShouldBe(0);
        using var doc = JsonDocument.Parse(r.Out);
        var root = doc.RootElement;
        root.GetProperty("coordinateType").GetString().ShouldBe("Pixel");
        root.GetProperty("unit").GetString().ShouldBe("Second");
        var walk = root.GetProperty("chains")[0];
        walk.GetProperty("name").GetString().ShouldBe("Walk");
        var frames = walk.GetProperty("frames");
        frames.GetArrayLength().ShouldBe(3);
        frames[2].GetProperty("index").GetInt32().ShouldBe(2);
        frames[2].GetProperty("texture").GetString().ShouldBe("sheet.png");
        frames[2].GetProperty("length").GetDouble().ShouldBe(0.2, 0.0001);
        var region = frames[2].GetProperty("region");
        region.GetProperty("x").GetDouble().ShouldBe(32);
        region.GetProperty("width").GetDouble().ShouldBe(16);
        var shapes = frames[2].GetProperty("shapes");
        shapes[0].GetProperty("type").GetString().ShouldBe("rect");
        shapes[0].GetProperty("name").GetString().ShouldBe("Hitbox");
        shapes[1].GetProperty("type").GetString().ShouldBe("circle");
    }

    private async Task SetOffsetAndFlip() =>
        (await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--ops", _w.Write("ops.json", """
            {"ops":[
              {"op":"set-frame-flip","frame":"Walk/2","flipH":true,"flipDiagonal":true},
              {"op":"set-frame-offset","frame":"Walk/2","x":3,"y":-2.5}
            ]}
            """))).Code.ShouldBe(0);

    [Fact]
    public async Task Text_ShowsOffsetAndFlipOnlyWhenNonDefault()
    {
        await SetOffsetAndFlip();

        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"));

        var lines = r.Out.Split('\n');
        var walk2 = string.Join(" ", lines.SkipWhile(l => !l.StartsWith("  2  ")).Skip(1).TakeWhile(l => l.StartsWith("       ")));
        walk2.ShouldContain("offset 3,-2.5");
        walk2.ShouldContain("flip H D");
        var walk0 = string.Join(" ", lines.SkipWhile(l => !l.StartsWith("  0  ")).Skip(1).TakeWhile(l => l.StartsWith("       ")));
        walk0.ShouldNotContain("offset");
        walk0.ShouldNotContain("flip");
    }

    [Fact]
    public async Task Json_HasOffsetAndFlipFlagsOnEveryFrame()
    {
        await SetOffsetAndFlip();

        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json");

        using var doc = JsonDocument.Parse(r.Out);
        var frames = doc.RootElement.GetProperty("chains")[0].GetProperty("frames");
        var f2 = frames[2];
        f2.GetProperty("offset").GetProperty("x").GetDouble().ShouldBe(3);
        f2.GetProperty("offset").GetProperty("y").GetDouble().ShouldBe(-2.5);
        f2.GetProperty("flipHorizontal").GetBoolean().ShouldBeTrue();
        f2.GetProperty("flipVertical").GetBoolean().ShouldBeFalse();
        f2.GetProperty("flipDiagonal").GetBoolean().ShouldBeTrue();
        frames[0].GetProperty("offset").GetProperty("x").GetDouble().ShouldBe(0);
        frames[0].GetProperty("flipHorizontal").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task MissingFile_ExitsNonZeroWithMessage()
    {
        var r = await _w.RunAsync("describe", _w.PathOf("nope.achx"));

        r.Code.ShouldNotBe(0);
        r.Err.ShouldContain("nope.achx");
    }

    [Fact]
    public async Task NoFileArgument_IsUsageError()
    {
        var r = await _w.RunAsync("describe");

        r.Code.ShouldBe(2);
        r.Err.ShouldContain("describe");
    }

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task Locked_InTextOnlyWhenSet_InJsonAlways(string file)
    {
        (await _w.RunAsync("edit", _w.PathOf(file), "--op", "set-chain-locked Walk value=true")).Code.ShouldBe(0);

        foreach (var args in new string[][] { [], ["--summary"] })
        {
            var text = await _w.RunAsync(["describe", _w.PathOf(file), .. args]);
            var json = await _w.RunAsync(["describe", _w.PathOf(file), "--json", .. args]);

            var lines = text.Out.Split('\n');
            lines.Single(l => l.StartsWith("Walk  ")).ShouldContain("locked");
            lines.Single(l => l.StartsWith("Idle  ")).ShouldNotContain("locked");
            using var doc = JsonDocument.Parse(json.Out);
            doc.RootElement.GetProperty("chains").EnumerateArray().Select(c => c.GetProperty("locked").GetBoolean()).ShouldBe([true, false]);
        }
    }

    [Fact]
    public async Task NegativeZero_PrintsAsZero_TextAndJson()
    {
        // A flip of 0 stores -0; -0.0001 rounds to it.
        _w.Write("neg.achj", """
            { "fileRelativeTextures": true, "timeMeasurementUnit": "Second", "coordinateType": "Pixel",
              "animationChains": [ { "name": "Walk", "frames": [
                { "textureName": "sheet.png", "frameLength": 0.1, "leftCoordinate": 0, "rightCoordinate": 16, "topCoordinate": 0, "bottomCoordinate": 32,
                  "relativeX": -0.0, "relativeY": 5, "shapes": {
                  "rectangles": [ { "name": "Hit", "x": -0.0, "y": -0.0001, "scaleX": 8, "scaleY": 8 } ],
                  "polygons": [ { "name": "Pol", "x": -0.0, "y": 0, "points": [ { "x": -0.0, "y": 1 }, { "x": 2, "y": -0.0 }, { "x": 3, "y": 3 } ] } ] } } ] } ] }
            """);

        foreach (string[] command in new string[][] { ["describe", _w.PathOf("neg.achj")], ["at", _w.PathOf("neg.achj"), "--time", "0"] })
        {
            var text = await _w.RunAsync(command);
            var json = await _w.RunAsync([.. command, "--json"]);

            text.Code.ShouldBe(0, text.Err);
            text.Out.ShouldContain("offset 0,5");
            text.Out.ShouldMatch("Hit +at 0,0 scale 8x8");
            text.Out.ShouldContain("points 0,1 2,0 3,3");
            json.Out.ShouldContain("\"Pol\"");
            json.Out.ShouldNotMatch(@"-0(?![.\d])");
        }
    }
}
