using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>#1829: each source file of a sequence keeps its own unit; a sequence plays them all in seconds.</summary>
public class FrameLengthUnitSequenceTests
{
    // "ms.achx": Walk of two 100 ms frames. "sec.achx": Walk of two 0.1 s frames.
    private sealed class Lookup : ISequenceChainLookup
    {
        private readonly Dictionary<string, (AnimationChainSave Chain, FrameLengthUnit Unit)> _sources = new()
        {
            ["ms.achx"] = (Chain(100f, 100f), FrameLengthUnit.Milliseconds),
            ["sec.achx"] = (Chain(0.1f, 0.1f), FrameLengthUnit.Seconds),
        };

        private static AnimationChainSave Chain(params float[] lengths)
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            for (int i = 0; i < lengths.Length; i++)
                chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = lengths[i], LeftCoordinate = i * 0.5f, RightCoordinate = i * 0.5f + 0.5f, BottomCoordinate = 1 });
            return chain;
        }

        public AnimationChainSave? FindChain(SequenceChainReferenceSave reference) =>
            _sources.TryGetValue(reference.Source, out var found) ? found.Chain : null;

        public string? ResolveTexturePath(string source, AnimationFrameSave frame) => frame.TextureName;

        public FrameLengthUnit UnitOf(SequenceChainReferenceSave reference) =>
            _sources.TryGetValue(reference.Source, out var found) ? found.Unit : FrameLengthUnit.Seconds;
    }

    private static SequenceTrackSave Track(string source) =>
        new() { Name = source, Chains = { new SequenceChainReferenceSave { Source = source, Chain = "Walk" } } };

    [Fact]
    public void Scene_PlaysMillisecondAndSecondSourcesAtTheSameSeconds()
    {
        var sequence = new SequenceSave { Name = "Both", Tracks = { Track("ms.achx"), Track("sec.achx") } };

        var sprites = SequenceScene.At(sequence, 0, 0.15, new Lookup());

        sprites.Count.ShouldBe(2);
        sprites.ShouldAllBe(sprite => sprite.FrameIndex == 1);
        sprites.ShouldAllBe(sprite => Math.Abs(sprite.ElapsedInFrame - 0.05) < 1e-3);
    }

    [Fact]
    public void Scene_ReportsEachSourcesUnitOnItsSprites()
    {
        var sequence = new SequenceSave { Name = "Both", Tracks = { Track("ms.achx"), Track("sec.achx") } };

        var sprites = SequenceScene.At(sequence, 0, 0.05, new Lookup());

        sprites.Select(sprite => sprite.Unit.IsMilliseconds).OrderBy(x => x).ShouldBe(new[] { false, true });
    }

    [Fact]
    public void Reference_ToAMillisecondSequence_ShowsTheSamePoseAsTheSecondsOne()
    {
        var lookup = new Lookup();
        SequenceSave Clip(string name, string source) => new() { Name = name, Tracks = { Track(source) } };
        var msClip = Clip("MsClip", "ms.achx");
        var secClip = Clip("SecClip", "sec.achx");
        var file = new List<SequenceSave> { msClip, secClip };
        var reference = new SequenceReferenceSave { Sequence = "x" };

        var msPose = ReferenceFilmstrip.PoseAt(reference, msClip, 0.15, lookup, file);
        var secPose = ReferenceFilmstrip.PoseAt(reference, secClip, 0.15, lookup, file);

        msPose.ShouldNotBeNull();
        secPose.ShouldNotBeNull();
        msPose!.Layers.Count.ShouldBe(secPose!.Layers.Count);
        msPose.Layers[0].Left.ShouldBe(secPose.Layers[0].Left);
        msPose.Layers[0].Left.ShouldBe(0.5f, "frame 1 at 0.15 s");
    }
}
