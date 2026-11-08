using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A sequence with Loop off stops at its end and holds the composite of its last instant.</summary>
public class SequenceHoldFinalPoseTests
{
    // body.achj: Walk, two 0.25 s frames (0.5 s). fx.achj: Spark, two 0.5 s frames (1 s).
    private static SequenceDocument Load(TestHelpers.TempDir dir, params SequenceSave[] sequences)
    {
        WriteSource(Path.Combine(dir.Path, "body.achj"), "Walk", 0.25f, 0.25f);
        WriteSource(Path.Combine(dir.Path, "fx.achj"), "Spark", 0.5f, 0.5f);
        var save = new SequenceListSave { Sources = { "body.achj", "fx.achj" } };
        save.Sequences.AddRange(sequences);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static void WriteSource(string path, string chain, params float[] lengths)
    {
        var list = new AnimationChainListSave();
        var save = new AnimationChainSave { Name = chain, Loop = true };
        foreach (float length in lengths)
            save.Frames.Add(new AnimationFrameSave { TextureName = "t.png", FrameLength = length, RightCoordinate = 1, BottomCoordinate = 1 });
        list.AnimationChains.Add(save);
        list.SaveJson(path);
    }

    private static SequenceSave Sequence(string name, bool loop, params (string Track, string Source, string Chain, double Start, float Loops)[] chains)
    {
        var sequence = new SequenceSave { Name = name, Loop = loop };
        foreach (var (track, source, chain, start, loops) in chains)
        {
            sequence.Tracks.Add(new SequenceTrackSave
            {
                Name = track,
                Chains = { new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Loops = loops } },
            });
        }
        return sequence;
    }

    private static string Describe(IEnumerable<SequenceSprite> sprites) =>
        string.Join(", ", sprites.Select(s => $"{s.Track}:{s.Chain.Name}#{s.FrameIndex}@{s.ElapsedInFrame:0.###}"));

    [Fact]
    public void At_ANonLoopingSequence_AtAndPastItsLength_HoldsThePoseOfItsLastInstant()
    {
        using var dir = new TestHelpers.TempDir();
        // Walk twice: 1 s long, the last frame (index 1) is showing at the end.
        var document = Load(dir, Sequence("Attack", loop: false, ("Body", "body.achj", "Walk", 0, 2)));

        var atLength = SequenceScene.At(document, 0, 1.0);
        var past = SequenceScene.At(document, 0, 6.0);

        atLength.Single().FrameIndex.ShouldBe(1);
        Describe(atLength).ShouldBe(Describe(SequenceScene.At(document, 0, 0.9999)));
        Describe(past).ShouldBe(Describe(atLength));
    }

    [Fact]
    public void At_ANonLoopingSequence_DoesNotHoldATrackWhoseChainFinishedBeforeTheEnd()
    {
        using var dir = new TestHelpers.TempDir();
        // Body ends at 0.5 s, Fx at 1 s (the sequence length): the held pose is Fx alone.
        var document = Load(dir, Sequence("Attack", loop: false,
            ("Body", "body.achj", "Walk", 0, 1), ("Fx", "fx.achj", "Spark", 0, 1)));

        SequenceScene.At(document, 0, 0.3).Select(s => s.Chain.Name).ShouldBe(new[] { "Spark", "Walk" });
        SequenceScene.At(document, 0, 1.0).Select(s => s.Chain.Name).ShouldBe(new[] { "Spark" });
        SequenceScene.At(document, 0, 4.0).Select(s => s.Chain.Name).ShouldBe(new[] { "Spark" });
    }

    [Fact]
    public void At_ALoopingSequence_IsUnchanged_NothingShowsAtTheEndInstant()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Sequence("Attack", loop: true, ("Body", "body.achj", "Walk", 0, 2)));

        SequenceScene.At(document, 0, 0.3).Single().FrameIndex.ShouldBe(1);
        SequenceScene.At(document, 0, 1.0).ShouldBeEmpty();
    }

    [Fact]
    public void At_TwoSequences_EachFollowsItsOwnLoopFlag()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir,
            Sequence("Looping", loop: true, ("Body", "body.achj", "Walk", 0, 1)),
            Sequence("Once", loop: false, ("Fx", "fx.achj", "Spark", 0, 1)));

        // Walk is 0.5 s, Spark 1 s. Both playheads are at their own ends.
        var sprites = SequenceScene.At(document, new[] { new SequenceTime(0, 0.5), new SequenceTime(1, 1.0) });

        sprites.Select(s => $"{s.Sequence}:{s.Chain.Name}").ShouldBe(new[] { "1:Spark" });
    }

    [Fact]
    public void At_AnEmptyOrZeroLengthNonLoopingSequence_ShowsNothingAndDoesNotCrash()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir,
            Sequence("Empty", loop: false),
            Sequence("Missing", loop: false, ("Body", "body.achj", "Gone", 0, 1)));

        SequenceScene.At(document, 0, 0).ShouldBeEmpty();
        SequenceScene.At(document, 0, 5).ShouldBeEmpty();
        SequenceScene.At(document, 1, 0).ShouldBeEmpty();
        SequenceScene.At(document, 1, 5).ShouldBeEmpty();
    }
}
