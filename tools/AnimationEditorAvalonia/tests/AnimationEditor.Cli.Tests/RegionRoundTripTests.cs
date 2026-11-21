using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>What describe prints for a frame region must be accepted unchanged by the ops that take one (#1860).</summary>
public class RegionRoundTripTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "sheet.png");

    public void Dispose() => _w.Dispose();

    private async Task<string> TextRegion(string frameLinePrefix)
    {
        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"));
        var line = r.Out.Split('\n').First(l => l.TrimStart().StartsWith(frameLinePrefix, StringComparison.Ordinal));
        return Regex.Match(line, @"region (\S+)").Groups[1].Value;
    }

    private async Task<JsonElement> JsonRegion(string chain, int frame)
    {
        var r = await _w.RunAsync("describe", _w.PathOf("hero.achx"), "--json");
        var chains = JsonDocument.Parse(r.Out).RootElement.GetProperty("chains");
        return chains.EnumerateArray().First(c => c.GetProperty("name").GetString() == chain)
            .GetProperty("frames")[frame].GetProperty("region").Clone();
    }

    private async Task<CliOutput> Edit(string opsJson) =>
        await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--ops", _w.Write("ops.json", opsJson));

    [Fact]
    public async Task Text_PrintsXYWidthHeight()
    {
        // The Walk frame 2 cell is the 16 wide column starting at x=32.
        var text = await TextRegion("2  sheet.png");
        var parts = text.Split(',');
        parts[0].ShouldBe("32");
        parts[2].ShouldBe("16");
    }

    [Fact]
    public async Task Json_HasXYWidthHeight()
    {
        var region = await JsonRegion("Walk", 2);
        region.GetProperty("x").GetDouble().ShouldBe(32);
        region.GetProperty("width").GetDouble().ShouldBe(16);
        region.TryGetProperty("left", out _).ShouldBeFalse();
        region.TryGetProperty("right", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DescribeText_PastedIntoSetFrameRegion_GivesTheSameRegion()
    {
        var text = await TextRegion("2  sheet.png");

        var r = await Edit($$"""{"ops":[{"op":"set-frame-region","frame":"Walk/0","region":"{{text}}"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var a = await JsonRegion("Walk", 0);
        var b = await JsonRegion("Walk", 2);
        foreach (var f in new[] { "x", "y", "width", "height" })
            a.GetProperty(f).GetDouble().ShouldBe(b.GetProperty(f).GetDouble(), 0.01);
    }

    [Fact]
    public async Task DescribeText_PastedIntoAddFrame_GivesTheSameRegion()
    {
        var text = await TextRegion("2  sheet.png");

        var r = await Edit($$"""{"ops":[{"op":"add-frame","chain":"Idle","texture":"sheet.png","region":"{{text}}"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        var a = await JsonRegion("Idle", 1);
        var b = await JsonRegion("Walk", 2);
        foreach (var f in new[] { "x", "y", "width", "height" })
            a.GetProperty(f).GetDouble().ShouldBe(b.GetProperty(f).GetDouble(), 0.01);
    }

    [Fact]
    public async Task DescribeJsonFields_BuiltIntoTheOpString_GiveTheSameRegion()
    {
        var b = await JsonRegion("Walk", 2);
        var s = string.Join(",", new[] { "x", "y", "width", "height" }.Select(f => b.GetProperty(f).GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var r = await Edit($$"""{"ops":[{"op":"set-frame-region","frame":"Walk/0","region":"{{s}}"}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        (await TextRegion("0  sheet.png")).ShouldBe(await TextRegion("2  sheet.png"));
    }
}
