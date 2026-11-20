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
