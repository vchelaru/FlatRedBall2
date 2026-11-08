using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// A ping-pong chain's cycle is twice its frames, and a reference's loops is the block's length in cycles: a chain with Loop on repeats across it, one with Loop
/// off plays once and holds its last frame.
/// The first round of sequence tests used neither, so both rules were unpinned.
/// </summary>
public class SequencePingPongAndLoopTests
{
    // Bounce: three 0.1 s frames, ping-pong, so one cycle is 0.6 s. Once: the same frames, no ping-pong, Loop off. Spin: Once with Loop on.
    private static SequenceDocument Load(TestHelpers.TempDir dir, params SequenceTrackSave[] tracks)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(Chain("Bounce", pingPong: true, loop: false));
        list.AnimationChains.Add(Chain("Once", pingPong: false, loop: false));
        list.AnimationChains.Add(Chain("Spin", pingPong: false, loop: true));
        list.SaveJson(Path.Combine(dir.Path, "hero.achj"));

        var save = new SequenceListSave { Sources = { "hero.achj" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static AnimationChainSave Chain(string name, bool pingPong, bool loop)
    {
        var chain = new AnimationChainSave { Name = name, PingPong = pingPong, Loop = loop };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "t.png", FrameLength = 0.1f, RightCoordinate = 1, BottomCoordinate = 1 });
        return chain;
    }

    private static SequenceTrackSave Track(string chain, double start, float loops) => new()
    {
        Name = "Body",
        Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = chain, Start = start, Loops = loops } },
    };

    [Fact]
    public void CycleLength_PingPongChain_IsTwiceItsFrames_AndAPlainChainIsJustItsFrames()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir);

        document.CycleLength(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Bounce" })!.Value.ShouldBe(0.6, 1e-6);
        document.CycleLength(new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Once" })!.Value.ShouldBe(0.3, 1e-6);
    }

    [Fact]
    public void LengthAndBlocks_PingPongWithFloatLoops_UseTheDoubledCycle()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Bounce", start: 1, loops: 2.5f));
        var sequence = document.Save.Sequences[0];

        SequenceTimeline.Length(sequence, r => document.CycleLength(r) ?? 0).ShouldBe(1 + 2.5 * 0.6, 1e-6);
        var block = TimelineLayout.Blocks(sequence, document.CycleLength).Single();
        block.Full.ShouldBe(2.5 * 0.6, 1e-6);
        block.CycleLength.ShouldBe(0.6, 1e-6);
    }

    [Fact]
    public void At_PingPongChain_PlaysForwardThenBackwardWithinOneCycle()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Bounce", start: 0, loops: 1));

        // Forward 0.0-0.3 shows frames 0,1,2; the way back 0.3-0.6 shows 2,1,0.
        SequenceScene.At(document, 0, 0.05).Single().FrameIndex.ShouldBe(0);
        SequenceScene.At(document, 0, 0.25).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 0.35).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 0.45).Single().FrameIndex.ShouldBe(1);
        SequenceScene.At(document, 0, 0.55).Single().FrameIndex.ShouldBe(0);
        SequenceScene.At(document, 0, 0.65).ShouldBeEmpty();
    }

    // Was At_ReferenceLoops_OverrideAChainWhoseOwnLoopFlagIsOff: loops no longer override the chain's Loop flag.
    [Fact]
    public void At_ReferenceLoops_OnAChainWhoseOwnLoopFlagIsOff_HoldTheLastFrameInsteadOfRepeating()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Once", start: 0, loops: 3));

        SequenceScene.At(document, 0, 0.25).Single().FrameIndex.ShouldBe(2);
        // 0.35 s is 0.05 s past the one cycle: the last frame stays, it does not restart at frame 0.
        SequenceScene.At(document, 0, 0.35).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 0.85).Single().FrameIndex.ShouldBe(2);
        // The block is three cycles (0.9 s) long.
        SequenceScene.At(document, 0, 0.95).ShouldBeEmpty();
    }

    [Fact]
    public void At_ReferenceLoops_OnALoopingChain_RestartItEachCycle()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, Track("Spin", start: 0, loops: 3));

        SequenceScene.At(document, 0, 0.25).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 0.35).Single().FrameIndex.ShouldBe(0);
        SequenceScene.At(document, 0, 0.85).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 0.95).ShouldBeEmpty();
    }

    [Fact]
    public void At_ANonLoopingPingPong_HoldsFrameZeroWhereItsCycleEnds()
    {
        using var dir = new TestHelpers.TempDir();
        // Bounce is 0.6 s per cycle (0 1 2 2 1 0) with Loop off; two cycles is 1.2 s.
        var document = Load(dir, Track("Bounce", start: 0, loops: 2));

        SequenceScene.At(document, 0, 0.55).Single().FrameIndex.ShouldBe(0);
        SequenceScene.At(document, 0, 0.8).Single().FrameIndex.ShouldBe(0);
        SequenceScene.At(document, 0, 1.0).Single().FrameIndex.ShouldBe(0);
        SequenceScene.At(document, 0, 1.25).ShouldBeEmpty();
    }
}
