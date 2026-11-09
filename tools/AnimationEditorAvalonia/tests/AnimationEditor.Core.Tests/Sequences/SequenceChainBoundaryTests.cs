using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A chain's start is inclusive and its end exclusive, even when float frame lengths do not sum exactly (#1763).</summary>
public class SequenceChainBoundaryTests
{
    // Loop.achj has `frames` frames of `length` seconds; Next.achj is a one-frame chain.
    private static SequenceDocument Load(TestHelpers.TempDir dir, int frames, float length, double nextStart, float loops)
    {
        var list = new AnimationChainListSave();
        list.AnimationChains.Add(Chain("Loop", frames, length));
        list.AnimationChains.Add(Chain("Next", 1, 1f));
        string source = Path.Combine(dir.Path, "a.achj");
        list.SaveJson(source);

        var sequence = new SequenceSave { Name = "Action", Loop = true };
        sequence.Tracks.Add(new SequenceTrackSave
        {
            Name = "A", Chains = { new SequenceChainReferenceSave { Source = "a.achj", Chain = "Loop", Start = 0, Loops = loops } },
        });
        sequence.Tracks.Add(new SequenceTrackSave
        {
            Name = "B", Chains = { new SequenceChainReferenceSave { Source = "a.achj", Chain = "Next", Start = nextStart, Loops = 1 } },
        });
        var save = new SequenceListSave { Sources = { "a.achj" } };
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static AnimationChainSave Chain(string name, int frames, float length)
    {
        var chain = new AnimationChainSave { Name = name, Loop = true };
        for (int i = 0; i < frames; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "t.png", FrameLength = length, RightCoordinate = 1, BottomCoordinate = 1 });
        return chain;
    }

    // The ends are the exact decimal values a person (or a snap to a rounded time) lands on.
    [Theory]
    [InlineData(8, 0.15f, 2, 2.4)]
    [InlineData(3, 0.1f, 1, 0.3)]
    [InlineData(3, 0.1f, 3, 0.9)]
    [InlineData(7, 0.7f, 2, 9.8)]
    [InlineData(5, 0.15f, 4, 3.0)]
    public void At_TheExactEndOfALoopingBlock_ShowsTheChainStartingThere_NotTheBlocksFirstFrame(int frames, float length, float loops, double end)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, frames, length, nextStart: end, loops);

        SequenceScene.At(document, 0, end).Select(s => s.Chain.Name).ShouldBe(new[] { "Next" });
    }
}
