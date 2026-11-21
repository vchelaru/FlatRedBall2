using System.Text.Json;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class SequenceOpsTests : IDisposable
{
    private readonly SeqWorkspace _w = new();

    public void Dispose() => _w.Dispose();

    private EditRunResult Apply(string opsJson)
    {
        var parsed = SequenceOps.Parse(opsJson);
        parsed.Error.ShouldBeNull();
        return SequenceOps.Apply(_w.Open(), parsed.Ops!);
    }

    private SequenceListSave Reload() => SequenceListSave.FromFile(_w.SeqPath);

    [Fact]
    public void Specs_CoverEveryOpOfTheSlice()
    {
        SequenceOps.Specs.Select(s => s.Name).ShouldBe(
        [
            "add-sequence", "rename-sequence", "delete-sequence", "reorder-sequence", "set-sequence-loop",
            "add-track", "rename-track", "delete-track", "reorder-track", "set-track-continuous", "add-reference-track",
            "add-source", "remove-source", "replace-source",
            "add-block", "set-block-start", "set-block-loops", "set-block-end", "delete-block",
        ], ignoreOrder: true);
    }

    [Fact]
    public void Parse_RejectsAnimationOps_AndUnknownFields()
    {
        SequenceOps.Parse("""{"ops":[{"op":"add-chain","name":"x"}]}""").Error!.ShouldContain("unknown op 'add-chain'");
        SequenceOps.Parse("""{"ops":[{"op":"rename-sequence","sequence":"Run","to":"X","bogus":1}]}""").Error!.ShouldContain("no field 'bogus'");
        SequenceOps.Parse("""{"ops":[{"op":"rename-sequence","sequence":"Run"}]}""").Error!.ShouldContain("needs the field 'to'");
    }

    [Fact]
    public void Apply_OrdersOps_AndSavesOnce()
    {
        var run = Apply("""
            {"ops":[
              {"op":"rename-sequence","sequence":"Run","to":"Sprint"},
              {"op":"add-track","sequence":"Sprint","name":"Arm"},
              {"op":"add-block","track":"Sprint/Arm","source":"hero.achj","chain":"Idle","start":0.25,"loops":2},
              {"op":"set-track-continuous","track":"Sprint/Arm","value":true},
              {"op":"set-sequence-loop","sequence":"Rest","value":true}
            ]}
            """);

        run.Success.ShouldBeTrue(string.Join("; ", run.Result.Errors));
        var save = Reload();
        save.Sequences.Select(s => s.Name).ShouldBe(["Sprint", "Rest"]);
        var arm = save.Sequences[0].Tracks[0];
        (arm.Name, arm.Continuous, arm.Chains.Single().Chain, arm.Chains.Single().Start, arm.Chains.Single().Loops)
            .ShouldBe(("Arm", true, "Idle", 0.25, 2f));
        save.Sequences[1].Loop.ShouldBeTrue();
    }

    [Fact]
    public void Apply_FailingOp_WritesNothing_AndNamesTheOp()
    {
        var before = _w.Bytes;

        var run = Apply("""
            {"ops":[
              {"op":"set-sequence-loop","sequence":"Run","value":false},
              {"op":"rename-sequence","sequence":"Nope","to":"X"}
            ]}
            """);

        run.Success.ShouldBeFalse();
        run.ErrorOps.ShouldBe([1]);
        run.Result.Errors[0].ShouldContain("op 1 (rename-sequence)");
        run.Result.HasError(EditErrorCode.NotFound).ShouldBeTrue();
        _w.Bytes.ShouldBe(before);
    }

    [Fact]
    public void Apply_NoChange_LeavesFileByteIdentical()
    {
        var before = _w.Bytes;
        var stamp = File.GetLastWriteTimeUtc(_w.SeqPath);

        var run = Apply("""
            {"ops":[
              {"op":"set-sequence-loop","sequence":"Run","value":true},
              {"op":"set-block-start","block":"Run/Body/1","start":1}
            ]}
            """);

        run.Success.ShouldBeTrue();
        run.Result.ChangedItems.ShouldBeEmpty();
        _w.Bytes.ShouldBe(before);
        File.GetLastWriteTimeUtc(_w.SeqPath).ShouldBe(stamp);
    }

    [Fact]
    public void Apply_EditPreservesUnknownProperties()
    {
        var save = SeqWorkspace.Build();
        save.UnknownProperties["custom"] = JsonValue("file");
        save.Sequences[0].UnknownProperties["note"] = JsonValue("seq");
        save.Sequences[0].Tracks[0].UnknownProperties["tint"] = JsonValue("track");
        save.Sequences[0].Tracks[0].Chains[0].UnknownProperties["tag"] = JsonValue("block");
        save.Save(_w.SeqPath);

        Apply("""{"ops":[{"op":"set-block-start","block":"Run/Body/0","start":0.1}]}""").Success.ShouldBeTrue();

        var text = _w.Json;
        foreach (var kept in new[] { "\"custom\": \"file\"", "\"note\": \"seq\"", "\"tint\": \"track\"", "\"tag\": \"block\"" })
            text.ShouldContain(kept);
        Reload().Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(0.1);
    }

    [Fact]
    public void Apply_SelectorsExpand_ForSettersAndDeletes()
    {
        var run = Apply("""
            {"ops":[
              {"op":"set-track-continuous","track":"Run/*","value":true},
              {"op":"set-block-loops","block":"Run/Body/*","loops":3},
              {"op":"delete-block","block":"Run/Body/0-1"},
              {"op":"set-sequence-loop","sequence":"R*","value":false}
            ]}
            """);

        run.Success.ShouldBeTrue(string.Join("; ", run.Result.Errors));
        var save = Reload();
        save.Sequences.ShouldAllBe(s => !s.Loop);
        save.Sequences[0].Tracks.ShouldAllBe(t => t.Continuous);
        save.Sequences[0].Tracks[0].Chains.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_DeleteTrackSelector_RemovesEveryMatch()
    {
        Apply("""{"ops":[{"op":"delete-track","track":"Run/*"}]}""").Success.ShouldBeTrue();

        Reload().Sequences[0].Tracks.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_SelectorMatchingNothing_FailsWithNoMatch_AndWritesNothing()
    {
        var before = _w.Bytes;

        var run = Apply("""{"ops":[{"op":"set-track-continuous","track":"Run/Z*","value":true}]}""");

        run.Success.ShouldBeFalse();
        run.Result.HasError(EditErrorCode.NoMatch).ShouldBeTrue();
        _w.Bytes.ShouldBe(before);
    }

    [Fact]
    public void Apply_OneFailingTargetOfASelector_FailsTheBatch()
    {
        var before = _w.Bytes;

        var run = Apply("""{"ops":[{"op":"set-block-start","block":"Run/*/1","start":2}]}""");

        run.Success.ShouldBeFalse();
        run.Result.HasError(EditErrorCode.IndexOutOfRange).ShouldBeTrue();
        _w.Bytes.ShouldBe(before);
    }

    [Fact]
    public void Apply_FileWithMissingSource_StillEditable_WithWarning()
    {
        var save = SeqWorkspace.Build();
        save.Sources.Add("gone.achj");
        save.Save(_w.SeqPath);

        var run = Apply("""{"ops":[{"op":"set-sequence-loop","sequence":"Run","value":false}]}""");

        run.Success.ShouldBeTrue(string.Join("; ", run.Result.Errors));
        run.Result.Warnings.ShouldContain(w => w.Contains("check error"));
        Reload().Sequences[0].Loop.ShouldBeFalse();
    }

    [Fact]
    public void Apply_AddReferenceTrack_ThenSetClip()
    {
        Apply("""
            {"ops":[
              {"op":"add-reference-track","sequence":"Rest","target":"Run","start":1},
              {"op":"set-block-loops","block":"Rest/Run/0","loops":2}
            ]}
            """).Success.ShouldBeTrue();

        var clip = Reload().Sequences[1].Tracks[0].Reference!;
        (clip.Sequence, clip.Start, clip.Loops).ShouldBe(("Run", 1.0, 2f));
    }

    [Fact]
    public void SetBlockEnd_ComputesLoops_KeepingFractions()
    {
        var run = Apply("""
            {"ops":[
              {"op":"set-block-end","block":"Run/Shadow/0","end":1.25},
              {"op":"set-block-end","block":"Run/Body/1","end":2.2}
            ]}
            """);

        run.Success.ShouldBeTrue(string.Join("; ", run.Result.Errors));
        var tracks = Reload().Sequences[0].Tracks;
        tracks[1].Chains[0].Loops.ShouldBe(2.5f, 1e-4f); // Idle is 0.5 s
        tracks[0].Chains[1].Loops.ShouldBe(3f, 1e-4f);   // starts at 1, Walk is 0.4 s
    }

    [Fact]
    public void SetBlockEnd_TakesSelectors_AndEveryBlockEndsAtTheTarget()
    {
        Apply("""{"ops":[{"op":"set-block-end","block":"Run/*/0","end":2}]}""").Success.ShouldBeTrue();

        var tracks = Reload().Sequences[0].Tracks;
        tracks[0].Chains[0].Loops.ShouldBe(5f, 1e-4f);
        tracks[1].Chains[0].Loops.ShouldBe(4f, 1e-4f);
    }

    [Theory]
    [InlineData("Run/Body/1", 1.0, EditErrorCode.InvalidValue)]
    [InlineData("Run/Body/1", 0.5, EditErrorCode.InvalidValue)]
    public void SetBlockEnd_EndNotAfterStart_Fails(string block, double end, EditErrorCode code)
    {
        var run = Apply($$"""{"ops":[{"op":"set-block-end","block":"{{block}}","end":{{end.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}""");

        run.Success.ShouldBeFalse();
        run.Result.CodeAt(0).ShouldBe(code);
    }

    [Fact]
    public void SetBlockEnd_OnAReferenceClip_IsRefused()
    {
        var run = Apply("""
            {"ops":[
              {"op":"add-reference-track","sequence":"Rest","target":"Run"},
              {"op":"set-block-end","block":"Rest/Run/0","end":3}
            ]}
            """);

        run.Success.ShouldBeFalse();
        run.Result.CodeAt(0).ShouldBe(EditErrorCode.WrongTrackKind);
    }

    private static System.Text.Json.Nodes.JsonNode JsonValue(string v) => System.Text.Json.Nodes.JsonValue.Create(v)!;
}
