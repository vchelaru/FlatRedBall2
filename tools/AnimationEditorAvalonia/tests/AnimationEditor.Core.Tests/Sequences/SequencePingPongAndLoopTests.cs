using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// A ping-pong chain's cycle is twice its frames, and a reference's loop count replaces the chain's own Loop flag.
/// The first round of sequence tests used neither, so both rules were unpinned.
/// </summary>
public class SequencePingPongAndLoopTests
{
    // Bounce: three 0.1 s frames, ping-pong, so one cycle is 0.6 s. Once: the same frames, no ping-pong, Loop off.
    private static SequenceDocument Load(TestHelpers.TempDir dir, params SequenceTrackSave[] tracks)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(Chain("Bounce", pingPong: true, loop: false));
        list.AnimationChains.Add(Chain("Once", pingPong: false, loop: false));
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

    [Fact]
    public void At_ReferenceLoops_OverrideAChainWhoseOwnLoopFlagIsOff()
    {
        using var dir = new TestHelpers.TempDir();
        // Once holds its last frame when played alone (Loop off); three loops must restart it each 0.3 s.
        var document = Load(dir, Track("Once", start: 0, loops: 3));

        SequenceScene.At(document, 0, 0.25).Single().FrameIndex.ShouldBe(2);
        // 0.35 s is 0.05 s into the second cycle: frame 0 again, not the held last frame.
        SequenceScene.At(document, 0, 0.35).Single().FrameIndex.ShouldBe(0);
        // 0.85 s is 0.25 s into the third and last cycle.
        SequenceScene.At(document, 0, 0.85).Single().FrameIndex.ShouldBe(2);
        SequenceScene.At(document, 0, 0.95).ShouldBeEmpty();
    }
}
