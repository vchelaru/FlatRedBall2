using System.Text.Json;
using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

/// <summary>edit --op and edit --ops - (#1907): the same edits as an ops file, driven by the op specs.</summary>
public class InlineOpsTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achx", "hero.achj", "sheet.png");

    public void Dispose() => _w.Dispose();

    private static string[] Inline(string file, params string[] ops) =>
        ["edit", file, .. ops.SelectMany(o => new[] { "--op", o })];

    [Theory]
    [InlineData("hero.achx")]
    [InlineData("hero.achj")]
    public async Task InlineOps_SaveTheSameBytesAsTheEquivalentOpsFile(string file)
    {
        var viaFile = _w.PathOf("via" + file);
        File.Copy(_w.PathOf(file), viaFile);
        var ops = _w.Write("ops.json", """
            {"ops":[
              {"op":"set-frame-offset","frame":"Walk/0","x":4,"y":-3},
              {"op":"set-chain-loop","chain":"Walk","value":false},
              {"op":"add-shape","frame":"Walk/0","type":"polygon","name":"Zone"},
              {"op":"set-polygon","shape":"Walk/0/polygon/Zone","points":[[0,0],[6,0],[6,6]]},
              {"op":"rename-chain","chain":"Walk","to":"Walk Cycle"}
            ]}
            """);
        (await _w.RunAsync("edit", viaFile, "--ops", ops)).Code.ShouldBe(0);

        var r = await _w.RunAsync(Inline(_w.PathOf(file),
            "set-frame-offset Walk/0 x=4 y=-3",
            "set-chain-loop Walk value=false",
            "add-shape Walk/0 type=polygon name=Zone",
            "set-polygon Walk/0/polygon/Zone points=[[0, 0], [6,0], [6,6]]",
            "rename-chain Walk to=\"Walk Cycle\""));

        r.Code.ShouldBe(0, r.Out + r.Err);
        _w.Bytes(file).ShouldBe(File.ReadAllBytes(viaFile));
    }

    [Fact]
    public async Task OpsDash_ReadsTheOpsFileFromStdin()
    {
        var r = await _w.RunWithStdinAsync("""{"ops":[{"op":"set-frame-offset","frame":"Walk/0","x":9}]}""",
            "edit", _w.PathOf("hero.achx"), "--ops", "-");

        r.Code.ShouldBe(0, r.Out + r.Err);
        AnimationFileReader.Read(_w.PathOf("hero.achx")).Document!.AnimationChains.Single(c => c.Name == "Walk")
            .Frames[0].RelativeX.ShouldBe(9);
    }

    [Fact]
    public async Task InlineOps_OnASequenceFile_UseTheSequenceOpSet()
    {
        var save = new SequenceListSave();
        save.Sources.Add("hero.achj");
        var seq = new SequenceSave { Name = "Run" };
        seq.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0 } },
        });
        save.Sequences.Add(seq);
        var path = _w.PathOf("hero.seqj");
        save.Save(path);

        var r = await _w.RunAsync(Inline(path, "set-block-start Run/Body/0 start=0.5", "set-sequence-loop Run value=false"));

        r.Code.ShouldBe(0, r.Out + r.Err);
        var doc = SequenceFileReader.Read(path).Document!;
        doc.Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(0.5);
    }

    [Theory]
    [InlineData("set-frame-offset", "Expected: set-frame-offset <frame> [x=<number>] [y=<number>]")]
    [InlineData("set-frame-offset Walk/0 z=1", "no field 'z'")]
    [InlineData("set-frame-offset Walk/0 x=abc", "must be a number")]
    [InlineData("set-frame-offset Walk/0 Walk/1", "unexpected 'Walk/1'")]
    [InlineData("set-frame-offset Walk/0 x=1 x=2", "given twice")]
    public async Task BadInlineOp_IsAUsageErrorShowingTheExpectedForm_AndWritesNothing(string op, string expected)
    {
        var before = _w.Bytes("hero.achx");

        var r = await _w.RunAsync(Inline(_w.PathOf("hero.achx"), op));

        r.Code.ShouldBe(2);
        r.Err.ShouldContain(expected);
        r.Err.ShouldContain("Expected: ");
        _w.Bytes("hero.achx").ShouldBe(before);
    }

    [Fact]
    public async Task UnclosedBracket_IsAUsageError()
    {
        var r = await _w.RunAsync(Inline(_w.PathOf("hero.achx"), "set-polygon Walk/0/polygon/Zone points=[[0,0]"));

        r.Code.ShouldBe(2);
        r.Err.ShouldContain("unclosed");
    }

    [Fact]
    public async Task BadInlineOp_WithJson_IsUsageErrorDocument()
    {
        var r = await _w.RunAsync([.. Inline(_w.PathOf("hero.achx"), "rename-chain Walk"), "--json"]);

        r.Code.ShouldBe(2);
        using var d = JsonDocument.Parse(r.Out);
        d.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("UsageError");
        d.RootElement.GetProperty("errors")[0].GetProperty("message").GetString()!.ShouldContain("rename-chain <chain> to=<string>");
    }

    [Fact]
    public async Task OpAndOpsTogether_AreAUsageError()
    {
        var r = await _w.RunAsync("edit", _w.PathOf("hero.achx"), "--op", "add-chain Run", "--ops", _w.Write("o.json", "{\"ops\":[]}"));

        r.Code.ShouldBe(2);
    }

    [Fact]
    public void Form_IsBuiltFromTheSpec_PrimaryUnkeyed()
    {
        InlineOps.Form(EditOps.Specs.Single(s => s.Name == "set-frame-color"))
            .ShouldBe("set-frame-color <frame> [red=<integer>] [green=<integer>] [blue=<integer>] [alpha=<integer>] [operation=<multiply|add>]");
    }

    [Fact]
    public async Task Schema_ShowsTheInlineFormOfEveryOp()
    {
        var text = await _w.RunAsync("--schema");
        var json = await _w.RunAsync("--schema", "--json");

        text.Out.ShouldContain("inline: set-frame-offset <frame> [x=<number>] [y=<number>]");
        text.Out.ShouldContain("inline: set-block-start <block> start=<number>");
        using var d = JsonDocument.Parse(json.Out);
        d.RootElement.GetProperty("ops").EnumerateArray().All(o => o.TryGetProperty("inline", out var _)).ShouldBeTrue();
        d.RootElement.GetProperty("sequenceOps").EnumerateArray().All(o => o.TryGetProperty("inline", out var _)).ShouldBeTrue();
    }

    [Fact]
    public async Task OpWithNoRequiredField_TakesItsFirstFieldUnkeyed()
    {
        var path = _w.PathOf("fresh.seqj");
        (await _w.RunAsync("new", path)).Code.ShouldBe(0);

        var r = await _w.RunAsync(Inline(path, "add-sequence Run", "add-sequence name=Idle", "add-sequence"));
        var twice = await _w.RunAsync(Inline(path, "add-sequence Run Idle"));

        r.Code.ShouldBe(0, r.Out + r.Err);
        SequenceFileReader.Read(path).Document!.Sequences.Select(s => s.Name).ShouldBe(["Sequence", "Run", "Idle", "Sequence 2"]);
        InlineOps.Form(SequenceOps.Specs.Single(s => s.Name == "add-sequence")).ShouldBe("add-sequence [<name>]");
        twice.Code.ShouldBe(2);
        twice.Err.ShouldContain("unexpected 'Idle'");
    }
}
