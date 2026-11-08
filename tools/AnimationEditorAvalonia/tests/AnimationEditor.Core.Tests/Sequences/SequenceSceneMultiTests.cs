using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Several sequences composited into one preview, each at its own time.</summary>
public class SequenceSceneMultiTests
{
    // hero.achj: Walk, two 0.25 s frames (0.5 s). fx.achj: Spark, two 0.5 s frames (1 s).
    // Sequence 0 "Attack": Walk on a track at Z 0, x 10. Sequence 1 "Aura": Spark on a track at Z 5, x -10.
    private static SequenceDocument Load(TestHelpers.TempDir dir)
    {
        WriteSource(Path.Combine(dir.Path, "hero.achj"), "Walk", 0.25f, 0.25f);
        WriteSource(Path.Combine(dir.Path, "fx.achj"), "Spark", 0.5f, 0.5f);
        var save = new SequenceListSave { Sources = { "hero.achj", "fx.achj" } };
        save.Sequences.Add(Sequence("Attack", loop: true, Track("Body", z: 0, x: 10, "hero.achj", "Walk")));
        save.Sequences.Add(Sequence("Aura", loop: false, Track("Glow", z: 5, x: -10, "fx.achj", "Spark")));
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

    private static SequenceSave Sequence(string name, bool loop, SequenceTrackSave track)
    {
        var sequence = new SequenceSave { Name = name, Loop = loop };
        sequence.Tracks.Add(track);
        return sequence;
    }

    private static SequenceTrackSave Track(string name, int z, float x, string source, string chain)
    {
        var track = new SequenceTrackSave { Name = name, Z = z, X = x };
        track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = 0, Loops = 1 });
        return track;
    }

    [Fact]
    public void EachSequence_IsSampledAtItsOwnTime()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        var sprites = SequenceScene.At(document, new[] { new SequenceTime(0, 0.3), new SequenceTime(1, 0.1) });

        sprites.Select(s => $"{s.Chain.Name}:{s.FrameIndex}").ShouldBe(new[] { "Walk:1", "Spark:0" });
        sprites.Select(s => s.Sequence).ShouldBe(new[] { 0, 1 });
    }

    [Fact]
    public void Sequences_DrawInTheGivenOrder_FirstAtTheBack_RegardlessOfTrackZ()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        // Aura's track has the higher Z, but Z only orders sprites inside one sequence.
        var auraFirst = SequenceScene.At(document, new[] { new SequenceTime(1, 0), new SequenceTime(0, 0) });
        var attackFirst = SequenceScene.At(document, new[] { new SequenceTime(0, 0), new SequenceTime(1, 0) });

        auraFirst.Select(s => s.Chain.Name).ShouldBe(new[] { "Spark", "Walk" });
        attackFirst.Select(s => s.Chain.Name).ShouldBe(new[] { "Walk", "Spark" });
    }

    [Fact]
    public void AllSequences_ShareTheEntityOrigin()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        var sprites = SequenceScene.At(document, new[] { new SequenceTime(0, 0), new SequenceTime(1, 0) });

        sprites.Select(s => s.Sample().OffsetX).ShouldBe(new[] { 10f, -10f });
    }

    [Fact]
    public void ASingleSequence_IsExactlyTheSingleSequenceScene()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        var many = SequenceScene.At(document, new[] { new SequenceTime(1, 0.6) });
        var one = SequenceScene.At(document, 1, 0.6);

        many.Select(s => (s.Track, s.FrameIndex, s.Sequence)).ShouldBe(one.Select(s => (s.Track, s.FrameIndex, s.Sequence)));
    }

    [Fact]
    public void ASequenceOutsideTheFile_IsLeftOut_AndNoSequencesIsEmpty()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        SequenceScene.At(document, new[] { new SequenceTime(7, 0), new SequenceTime(0, 0) }).Select(s => s.Chain.Name).ShouldBe(new[] { "Walk" });
        SequenceScene.At(document, new SequenceTime[0]).ShouldBeEmpty();
    }

    [Fact]
    public void SequenceLength_IsEachSequencesOwnLength()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        document.SequenceLength(0).ShouldBe(0.5, 1e-9);
        document.SequenceLength(1).ShouldBe(1, 1e-9);
        document.SequenceLength(2).ShouldBe(0);
        document.SequenceLength(-1).ShouldBe(0);
    }
}
