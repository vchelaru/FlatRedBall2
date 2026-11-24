using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Cli.Tests;

public class SequenceCommandTests : IDisposable
{
    private readonly CliWorkspace _w = new("hero.achj", "hero.achx", "sheet.png");

    public void Dispose() => _w.Dispose();

    private string WriteSeq(string name = "hero.seqj")
    {
        var save = new SequenceListSave();
        save.Sources.Add("hero.achj");
        var seq = new SequenceSave { Name = "Run" };
        seq.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Continuous = true,
            Chains =
            {
                new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0, Length = 0.8 },
                new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Idle", Start = 1 },
            },
        });
        save.Sequences.Add(seq);
        var path = _w.PathOf(name);
        save.Save(path);
        return path;
    }

    private async Task<CliOutput> Edit(string file, string opsJson, params string[] extra) =>
        await _w.RunAsync(["edit", file, "--ops", _w.Write("ops.json", opsJson), .. extra]);

    [Fact]
    public async Task Describe_Text_ShowsBlocksWithComputedEnd()
    {
        var file = WriteSeq();

        var r = await _w.RunAsync("describe", file);

        r.Code.ShouldBe(0, r.Err);
        r.Out.ShouldContain("Run  (loop, 1 track, length 1.5)");
        r.Out.ShouldContain("Body  continuous");
        r.Out.ShouldContain("Run/Body/0  Walk  hero.achj  start 0  length 0.8  end 0.8");
        r.Out.ShouldContain("Run/Body/1  Idle  hero.achj  start 1  length auto  end 1.5");
    }

    [Fact]
    public async Task Describe_Json_IsSequenceShaped()
    {
        var file = WriteSeq();

        var r = await _w.RunAsync("describe", file, "--json");

        r.Code.ShouldBe(0, r.Err);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("kind").GetString().ShouldBe("sequences");
        doc.RootElement.GetProperty("sequences")[0].GetProperty("tracks")[0].GetProperty("blocks").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Describe_ReadsTheRealCrankyChibiFileWithoutWriting()
    {
        var real = @"F:\GitHub\CrankyChibiCthulhu\CrankyChibiCthulu\Content\Entities\Bosses\ShubbyPhase2\ShubbyPhase2Sequences.seqj";
        if (!File.Exists(real)) return; // developer machine only
        var before = File.ReadAllBytes(real);

        var r = await _w.RunAsync("describe", real);

        r.Code.ShouldBe(0, r.Err);
        File.ReadAllBytes(real).ShouldBe(before);
    }

    [Fact]
    public async Task Check_Clean_ExitsZero()
    {
        var file = WriteSeq();

        var r = await _w.RunAsync("check", file);

        r.Code.ShouldBe(0, r.Out + r.Err);
        r.Out.ShouldContain("ok:");
    }

    [Fact]
    public async Task Check_BrokenReferences_ExitsOne_WithCodes()
    {
        var file = WriteSeq();
        var save = SequenceListSave.FromFile(file);
        save.Sequences[0].Tracks[0].Chains[0].Chain = "Fly";
        save.Sources.Add("gone.achj");
        save.Save(file);

        var r = await _w.RunAsync("check", file, "--json");

        r.Code.ShouldBe(1);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("code").GetString())
            .ShouldBe(["missing-source", "missing-chain"], ignoreOrder: true);
    }

    [Fact]
    public async Task Check_CutShortBlock_IsAWarningOnly()
    {
        var file = WriteSeq();
        var save = SequenceListSave.FromFile(file);
        save.Sequences[0].Tracks[0].Chains[1].Start = 0.5;
        save.Save(file);

        var r = await _w.RunAsync("check", file);

        r.Code.ShouldBe(0);
        r.Out.ShouldContain("warning[cut-short]");
    }

    [Fact]
    public async Task New_CreatesSequenceFileByExtension_NeverOverwrites()
    {
        var file = _w.PathOf("fresh.seqj");

        var first = await _w.RunAsync("new", file);
        var second = await _w.RunAsync("new", file);

        first.Code.ShouldBe(0, first.Out + first.Err);
        SequenceListSave.FromFile(file).Sequences.Select(s => s.Name).ShouldBe(["Sequence"]);
        second.Code.ShouldBe(1);
    }

    [Fact]
    public async Task Edit_AppliesSequenceOps_AndSaves()
    {
        var file = WriteSeq();

        var r = await Edit(file, """
            {"ops":[
              {"op":"add-sequence","name":"Idle"},
              {"op":"add-track","sequence":"Idle","name":"Body"},
              {"op":"add-block","track":"Idle/Body","source":"hero.achj","chain":"Idle","length":2},
              {"op":"set-track-continuous","track":"Run/Body","value":false}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var save = SequenceListSave.FromFile(file);
        save.Sequences.Select(s => s.Name).ShouldBe(["Run", "Idle"]);
        save.Sequences[1].Tracks[0].Chains[0].Length.ShouldBe(2);
        save.Sequences[0].Tracks[0].Continuous.ShouldBeFalse();
    }

    [Fact]
    public async Task Edit_SetBlockChain_RepointsChainAndSource_KeepingStartAndLength()
    {
        var file = WriteSeq();

        var r = await Edit(file, """
            {"ops":[
              {"op":"set-block-chain","block":"Run/Body/0","chain":"Idle"},
              {"op":"add-source","path":"hero.achx"},
              {"op":"set-block-chain","block":"Run/Body/1","source":"hero.achx","chain":"Walk"}
            ]}
            """);

        r.Code.ShouldBe(0, r.Out + r.Err);
        var blocks = SequenceListSave.FromFile(file).Sequences[0].Tracks[0].Chains;
        (blocks[0].Source, blocks[0].Chain, blocks[0].Start, blocks[0].Length).ShouldBe(("hero.achj", "Idle", 0d, (double?)0.8));
        (blocks[1].Source, blocks[1].Chain, blocks[1].Start, blocks[1].Length).ShouldBe(("hero.achx", "Walk", 1d, (double?)null));
    }

    [Fact]
    public async Task Edit_SetBlockChain_RefusesReferenceClip_UnknownChain_AndUnlistedSource()
    {
        var file = WriteSeq();
        var save = SequenceListSave.FromFile(file);
        save.Sequences.Add(new SequenceSave { Name = "Other" });
        save.Sequences[0].Tracks.Add(new SequenceTrackSave { Name = "Sub", Reference = new SequenceReferenceSave { Sequence = "Other" } });
        save.Save(file);
        var before = File.ReadAllBytes(file);

        var clip = await Edit(file, """{"ops":[{"op":"set-block-chain","block":"Run/Sub/0","chain":"Idle"}]}""", "--json");
        var chain = await Edit(file, """{"ops":[{"op":"set-block-chain","block":"Run/Body/0","chain":"Nope"}]}""");
        var source = await Edit(file, """{"ops":[{"op":"set-block-chain","block":"Run/Body/0","source":"x.achj","chain":"Idle"}]}""");

        clip.Code.ShouldBe(1);
        clip.Out.ShouldContain("WrongTrackKind");
        (chain.Out + chain.Err).ShouldContain("no chain named 'Nope'");
        (source.Out + source.Err).ShouldContain("not in the library");
        File.ReadAllBytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task Edit_Failure_WritesNothing_AndReportsCodeInJson()
    {
        var file = WriteSeq();
        var before = File.ReadAllBytes(file);

        var r = await Edit(file, """
            {"ops":[{"op":"set-sequence-loop","sequence":"Run","value":false},{"op":"delete-block","block":"Run/Body/9"}]}
            """, "--json");

        r.Code.ShouldBe(1);
        using var doc = JsonDocument.Parse(r.Out);
        doc.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("errors")[0].GetProperty("op").GetInt32().ShouldBe(1);
        doc.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("IndexOutOfRange");
        File.ReadAllBytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task Edit_NoChange_LeavesFileByteIdentical()
    {
        var file = WriteSeq();
        var before = File.ReadAllBytes(file);

        var r = await Edit(file, """{"ops":[{"op":"set-sequence-loop","sequence":"Run","value":true}]}""");

        r.Code.ShouldBe(0, r.Out + r.Err);
        r.Out.ShouldContain("No changes");
        File.ReadAllBytes(file).ShouldBe(before);
    }

    [Fact]
    public async Task Edit_UsesTheFileKindToChooseTheOpSet()
    {
        var seq = WriteSeq();

        var animationOpOnSequenceFile = await Edit(seq, """{"ops":[{"op":"add-chain","name":"x"}]}""");
        var sequenceOpOnAnimationFile = await Edit(_w.PathOf("hero.achj"), """{"ops":[{"op":"add-sequence"}]}""");

        animationOpOnSequenceFile.Code.ShouldBe(2);
        animationOpOnSequenceFile.Err.ShouldContain("unknown op 'add-chain'");
        sequenceOpOnAnimationFile.Code.ShouldBe(2);
        sequenceOpOnAnimationFile.Err.ShouldContain("unknown op 'add-sequence'");
    }

    [Fact]
    public async Task Schema_ListsSequenceOpsSeparately()
    {
        var text = await _w.RunAsync("--schema");
        var json = await _w.RunAsync("--schema", "--json");

        text.Out.ShouldContain("Sequence files (.seqj)");
        text.Out.ShouldContain("add-block");
        using var doc = JsonDocument.Parse(json.Out);
        var names = doc.RootElement.GetProperty("sequenceOps").EnumerateArray().Select(o => o.GetProperty("op").GetString()).ToArray();
        names.ShouldContain("add-block");
        names.ShouldContain("set-track-continuous");
        names.ShouldContain("add-reference-track");
        doc.RootElement.GetProperty("ops").EnumerateArray().Select(o => o.GetProperty("op").GetString()).ShouldNotContain("add-block");
        doc.RootElement.GetProperty("sequenceSelectors").GetString().ShouldNotBeNullOrEmpty();
    }
}
