using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class SequenceReportTests : IDisposable
{
    private readonly SeqWorkspace _w = new();

    public void Dispose() => _w.Dispose();

    private CheckReport Check(SequenceListSave save)
    {
        save.Save(_w.SeqPath);
        var read = SequenceFileReader.Read(_w.SeqPath);
        read.Document.ShouldNotBeNull(read.Error);
        return SequenceCheck.Run(read.Document!, _w.SeqPath);
    }

    [Fact]
    public void Check_CleanFile_HasNoIssues()
    {
        Check(SeqWorkspace.Build()).Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Check_MissingSourceFile_IsAnError()
    {
        var save = SeqWorkspace.Build();
        save.Sources.Add("gone.achj");

        var report = Check(save);

        report.Ok.ShouldBeFalse();
        report.Errors.Single().Code.ShouldBe("missing-source");
        report.Errors.Single().Message.ShouldContain("gone.achj");
    }

    [Fact]
    public void Check_BlockOfUnlistedSource_IsAnError()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks[0].Chains[0].Source = "villain.achj";

        Check(save).Errors.Select(i => i.Code).ShouldBe(["missing-source"]);
    }

    [Fact]
    public void Check_MissingChain_IsAnError_WithAddress()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks[0].Chains[1].Chain = "Fly";

        var issue = Check(save).Errors.Single();

        issue.Code.ShouldBe("missing-chain");
        issue.Message.ShouldContain("Run/Body/1");
        issue.Message.ShouldContain("Fly");
    }

    [Fact]
    public void Check_BlockCutShortByNextBlock_IsAWarning()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks[0].Chains[1].Start = 0.5;

        var report = Check(save);

        report.Ok.ShouldBeTrue();
        var warning = report.Warnings.Single();
        warning.Code.ShouldBe("cut-short");
        warning.Message.ShouldContain("Run/Body/0");
    }

    [Fact]
    public void Check_BlocksStartingTogether_FirstNeverPlays()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks[0].Chains[1].Start = 0;

        Check(save).Warnings.Select(i => i.Code).ShouldContain("unreachable-block");
    }

    [Fact]
    public void Check_ReferenceProblems_AreErrors()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks.Add(new SequenceTrackSave { Name = "Gone", Reference = new SequenceReferenceSave { Sequence = "Nope" } });
        save.Sequences[1].Tracks.Add(new SequenceTrackSave { Name = "Loop", Reference = new SequenceReferenceSave { Sequence = "Rest" } });

        Check(save).Errors.Select(i => i.Code).ShouldBe(["missing-reference-target", "reference-cycle"], ignoreOrder: true);
    }

    [Fact]
    public void Check_DuplicateNames_AreErrors()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[1].Name = "Run";
        save.Sequences[0].Tracks[1].Name = "Body";

        Check(save).Errors.Select(i => i.Code).ShouldBe(["duplicate-sequence-name", "duplicate-track-name"], ignoreOrder: true);
    }

    [Fact]
    public void Describe_Text_ListsSourcesSequencesTracksAndBlocks()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks[0].Continuous = true;
        save.Sources.Add("gone.achj");
        save.Save(_w.SeqPath);
        var doc = SequenceFileReader.Read(_w.SeqPath).Document!;

        var text = SequenceDescriber.ToText(doc, _w.SeqPath);

        text.ShouldContain("sheet.seqj");
        text.ShouldContain("hero.achj");
        text.ShouldContain("gone.achj  MISSING");
        text.ShouldContain("Run  (loop, 2 tracks, length 1.4)");
        text.ShouldContain("Rest  (no loop, 1 track, length 0.5)");
        text.ShouldContain("Body  continuous");
        text.ShouldContain("Run/Body/1  Walk  hero.achj  start 1  length auto  end 1.4");
        text.ShouldContain("Run/Shadow/0  Idle  hero.achj  start 0  length auto  end 0.5");
        text.ShouldContain("Run/Body/0  Walk  hero.achj  start 0  length 0.8  end 0.8");
    }

    [Fact]
    public void Describe_Text_FlagsMissingChainsAndShowsReferenceTracks()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks[0].Chains[1].Chain = "Fly";
        save.Sequences[1].Tracks.Add(new SequenceTrackSave { Name = "Inner", Reference = new SequenceReferenceSave { Sequence = "Run", Start = 0.5, Length = 2 } });
        save.Save(_w.SeqPath);
        var doc = SequenceFileReader.Read(_w.SeqPath).Document!;

        var text = SequenceDescriber.ToText(doc, _w.SeqPath);

        text.ShouldContain("Run/Body/1  Fly  hero.achj  start 1  length auto  end 1  MISSING chain");
        text.ShouldContain("Rest/Inner/0  plays Run  start 0.5  length 2");
    }

    [Fact]
    public void Describe_Json_HasTheSameFacts()
    {
        var save = SeqWorkspace.Build();
        save.Sequences[0].Tracks[0].Continuous = true;
        save.Sources.Add("gone.achj");
        save.Save(_w.SeqPath);
        var doc = SequenceFileReader.Read(_w.SeqPath).Document!;

        using var json = JsonDocument.Parse(EditJson.Render(w => SequenceDescriber.WriteJson(w, doc, _w.SeqPath)));

        var root = json.RootElement;
        root.GetProperty("kind").GetString().ShouldBe("sequences");
        root.GetProperty("sources").EnumerateArray().Select(s => (s.GetProperty("path").GetString(), s.GetProperty("exists").GetBoolean()))
            .ShouldBe([("hero.achj", true), ("gone.achj", false)]);
        var run = root.GetProperty("sequences")[0];
        run.GetProperty("name").GetString().ShouldBe("Run");
        run.GetProperty("loop").GetBoolean().ShouldBeTrue();
        run.GetProperty("length").GetDouble().ShouldBe(1.4, 1e-6);
        var body = run.GetProperty("tracks")[0];
        body.GetProperty("continuous").GetBoolean().ShouldBeTrue();
        var block = body.GetProperty("blocks")[1];
        block.GetProperty("chain").GetString().ShouldBe("Walk");
        block.GetProperty("start").GetDouble().ShouldBe(1.0, 1e-6);
        block.GetProperty("length").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("blocks")[0].GetProperty("length").GetDouble().ShouldBe(0.8, 1e-6);
        block.GetProperty("end").GetDouble().ShouldBe(1.4, 1e-6);
    }

    private static SequenceListSave WithContinuousClip(bool targetLoops, double? clipLength, double start = 0)
    {
        var save = SeqWorkspace.Build();
        var target = new SequenceSave { Name = "Tgt", Loop = targetLoops };
        target.Tracks.Add(new SequenceTrackSave { Name = "Fx", Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Idle", Start = 0 } } });
        save.Sequences.Add(target);
        save.Sequences[0].Tracks.Add(new SequenceTrackSave
        {
            Name = "Clip", Continuous = true,
            Reference = new SequenceReferenceSave { Sequence = "Tgt", Start = start, Length = clipLength },
        });
        return save;
    }

    [Fact]
    public void Check_ContinuousReferenceThatCannotCarry_WarnsWhy()
    {
        Check(WithContinuousClip(targetLoops: false, clipLength: 100)).Warnings.Single(i => i.Code == "continuous-ignored").Message.ShouldContain("'Tgt' does not loop");
        Check(WithContinuousClip(targetLoops: true, clipLength: 0.1)).Warnings.Single(i => i.Code == "continuous-ignored").Message.ShouldContain("set the clip length to");
        Check(WithContinuousClip(targetLoops: true, clipLength: 100, start: 0.5)).Warnings.Single(i => i.Code == "continuous-ignored").Message.ShouldContain("not 0");
    }

    [Fact]
    public void Check_ContinuousReferenceThatCarries_HasNoWarning()
    {
        Check(WithContinuousClip(targetLoops: true, clipLength: 100)).Warnings.Select(i => i.Code).ShouldNotContain("continuous-ignored");
    }
}
