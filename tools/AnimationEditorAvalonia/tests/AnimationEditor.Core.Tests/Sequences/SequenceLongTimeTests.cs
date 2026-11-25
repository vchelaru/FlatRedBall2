using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Sampling far into a long block.</summary>
public class SequenceLongTimeTests
{
    private static AnimationChainSave Walk(params float[] lengths)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        foreach (float length in lengths)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = length, RightCoordinate = 16, BottomCoordinate = 16 });
        return chain;
    }

    private static SequenceDocument Load(TestHelpers.TempDir dir, string sequences)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(Walk(0.1f, 0.1f, 0.1f, 0.1f, 0.1f));
        list.Save(Path.Combine(dir.Path, "hero.achx"));
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, "{ \"version\": 1, \"sources\": [\"hero.achx\"], \"sequences\": [ " + sequences + " ] }");
        return SequenceDocument.Load(path);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(400)]
    public void SceneAt_StartOfALaterCycle_ShowsTheFirstFrame(double time)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, """
            { "name": "A", "tracks": [ { "name": "T", "chains": [
              { "source": "hero.achx", "chain": "Walk", "start": 0, "length": 1000 } ] } ] }
            """);

        SequenceScene.At(document, 0, time).Single().FrameIndex.ShouldBe(0);
    }

    // The clip's target is one 0.5 s cycle, so its own wraps add up the same float error.
    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(400)]
    public void SceneAt_StartOfALaterRunOfAPlayedSequence_ShowsTheFirstFrame(double time)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, """
            { "name": "A", "loop": true, "tracks": [ { "name": "T", "chains": [ { "source": "hero.achx", "chain": "Walk", "start": 0 } ] } ] },
            { "name": "Host", "loop": true, "tracks": [ { "name": "R", "references": [ { "sequence": "A", "start": 0, "length": 1000 } ] } ] }
            """);

        SequenceScene.At(document, 1, time).Single().FrameIndex.ShouldBe(0);
    }

    // 0.1f is a hair over 0.1 and 0.7f a hair under 0.7, so the summed boundary lands late in one and early in the other.
    [Theory]
    [InlineData(0.1f, 0.1, 100)]
    [InlineData(0.1f, 0.1, 100_000)]
    [InlineData(0.7f, 0.7, 700)]
    [InlineData(0.7f, 0.7, 70_000)]
    public void Locate_FrameBoundaryLateInALoop_ShowsTheFrameThatStartsThere_AndNotBeforeIt(float length, double step, double cycleStart)
    {
        var chain = Walk(length, length);
        int FrameAt(double time) => ChainClock.Locate(chain, time, FrameLengthUnit.Seconds).FrameIndex;

        FrameAt(cycleStart).ShouldBe(0);
        FrameAt(cycleStart + step).ShouldBe(1);
        FrameAt(cycleStart + step - 0.03).ShouldBe(0);
        FrameAt(cycleStart + 2 * step - 0.03).ShouldBe(1);
    }
}
