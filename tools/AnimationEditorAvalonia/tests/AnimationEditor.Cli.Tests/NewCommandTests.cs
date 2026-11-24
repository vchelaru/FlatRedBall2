using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class NewCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx");

    public void Dispose() => _w.Dispose();

    [Theory]
    [InlineData("fresh.achx")]
    [InlineData("fresh.achj")]
    public async Task CreatesAnEmptyFileThatChecksClean(string name)
    {
        var path = _w.PathOf(name);

        var r = await _w.RunAsync("new", path);

        r.Code.ShouldBe(0);
        File.Exists(path).ShouldBeTrue();
        (await _w.RunAsync("check", path)).Code.ShouldBe(0);
    }

    [Fact]
    public async Task ExistingFile_FailsAndLeavesItUntouched()
    {
        var before = _w.Bytes("hero.achx");

        var r = await _w.RunAsync("new", _w.PathOf("hero.achx"));

        r.Code.ShouldBe(1);
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task Json_ReportsTheCreatedPath()
    {
        var r = await _w.RunAsync("new", _w.PathOf("j.achx"), "--json");

        r.Code.ShouldBe(0);
        r.Out.ShouldContain("j.achx");
        r.Out.ShouldContain("\"success\":true");
    }

    [Fact]
    public async Task SequenceFile_NamesTheSequenceItCreated()
    {
        var text = await _w.RunAsync("new", _w.PathOf("a.seqj"));
        var json = await _w.RunAsync("new", _w.PathOf("b.seqj"), "--json");
        var animation = await _w.RunAsync("new", _w.PathOf("c.achx"), "--json");

        text.Out.ShouldContain("a.seqj with sequence 'Sequence'");
        json.Out.ShouldContain("\"sequence\":\"Sequence\"");
        animation.Out.ShouldNotContain("\"sequence\"");
    }
}
