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
        region.GetProperty("left").GetDouble().ShouldBe(32);
        region.GetProperty("right").GetDouble().ShouldBe(48);
        var shapes = frames[2].GetProperty("shapes");
        shapes[0].GetProperty("type").GetString().ShouldBe("rect");
        shapes[0].GetProperty("name").GetString().ShouldBe("Hitbox");
        shapes[1].GetProperty("type").GetString().ShouldBe("circle");
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
}
