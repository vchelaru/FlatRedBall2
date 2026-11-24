using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

// Issue #1949: renaming or deleting a chain names the blocks of same-folder .seqj files it left without a chain.
public class SequenceUsageWarningTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private string WriteSeq(string name, params (string Source, string Chain)[] blocks)
    {
        var track = new SequenceTrackSave { Name = "Body" };
        foreach (var (source, chain) in blocks)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain });
        var save = new SequenceListSave();
        save.Sources.AddRange(blocks.Select(b => b.Source).Distinct());
        save.Sequences.Add(new SequenceSave { Name = "Run", Tracks = { track } });
        var path = _w.PathOf(name);
        save.Save(path);
        return path;
    }

    private async Task<string[]> WarningsOf(params string[] ops)
    {
        string[] inline = [.. ops.SelectMany(op => new[] { "--op", op })];
        var r = await _w.RunAsync(["edit", _w.PathOf("hero.achx"), .. inline, "--json"]);
        r.Code.ShouldBe(0, r.Out);
        using var doc = JsonDocument.Parse(r.Out);
        return doc.RootElement.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToArray();
    }

    [Fact]
    public async Task Edit_RenameAndDeleteChain_WarnsOncePerSequenceFileAndWritesNoneOfThem()
    {
        WriteSeq("b.seqj", ("hero.achx", "Idle"));
        WriteSeq("a.seqj", ("hero.achx", "Walk"), ("hero.achj", "Walk"), ("hero.achx", "Idle"));
        Directory.CreateDirectory(_w.PathOf("sub"));
        WriteSeq("sub/c.seqj", ("../hero.achx", "Walk"));
        _w.Write("bad.seqj", "{ not json");
        var before = _w.Bytes("a.seqj");

        var warnings = await WarningsOf("rename-chain Walk to=Stroll", "delete-chain Idle");

        warnings.ShouldBe([
            "a.seqj now has 2 block(s) playing a chain that hero.achx no longer has: Run/Body/0 'Walk', Run/Body/2 'Idle'.",
            "b.seqj now has 1 block(s) playing a chain that hero.achx no longer has: Run/Body/0 'Idle'.",
        ]);
        _w.Bytes("a.seqj").ShouldBe(before);
    }

    [Fact]
    public async Task Edit_Text_PrintsTheWarningAndSucceeds()
    {
        WriteSeq("a.seqj", ("hero.achx", "Walk"));

        var r = await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--op", "rename-chain Walk to=Stroll");

        r.Code.ShouldBe(0);
        r.Out.ShouldContain("warning: a.seqj now has 1 block(s) playing a chain that hero.achx no longer has: Run/Body/0 'Walk'.");
    }

    [Fact]
    public async Task Edit_ManyBrokenBlocks_ListsTenAndCountsTheRest()
    {
        WriteSeq("a.seqj", [.. Enumerable.Repeat(("hero.achx", "Walk"), 12)]);

        var warning = (await WarningsOf("delete-chain Walk")).ShouldHaveSingleItem();

        warning.ShouldStartWith("a.seqj now has 12 block(s)");
        warning.ShouldEndWith("Run/Body/9 'Walk' and 2 more.");
    }

    [Theory]
    [InlineData("rename-chain Walk to=Stroll", "rename-chain Stroll to=Walk", "set-frame-offset Walk/0 x=5")]
    [InlineData("delete-chain Walk", "duplicate-chain Idle name=Walk")]
    [InlineData("add-chain Gone", "delete-chain Gone", "set-frame-offset Walk/0 x=5")]
    [InlineData("rename-chain Walk to=Temp", "rename-chain Idle to=Walk", "rename-chain Temp to=Idle")]
    public async Task Edit_NoNameTheFileHadIsMissingAfterwards_SaysNothing(params string[] ops)
    {
        WriteSeq("a.seqj", ("hero.achx", "Walk"), ("hero.achx", "Idle"), ("hero.achx", "Gone"));

        (await WarningsOf(ops)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_BatchThatFails_DoesNotWarn()
    {
        WriteSeq("a.seqj", ("hero.achx", "Walk"));

        var r = await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--op", "delete-chain Walk", "--op", "delete-chain Nope");

        r.Code.ShouldBe(1);
        r.Out.ShouldNotContain("a.seqj");
    }
}
