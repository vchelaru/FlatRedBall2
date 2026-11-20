using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The composite filmstrip a collapsed reference clip draws: where its cells are and what each shows.</summary>
public class ReferenceFilmstripTests
{
    // One chain "Walk" of two 0.5 s frames (so a 1 s cycle), "Run" of one 1 s frame; textures resolve to one path per chain.
    private sealed class Lookup : ISequenceChainLookup
    {
        private readonly Dictionary<string, AnimationChainSave> _chains = new();

        public Lookup()
        {
            _chains["Walk"] = Chain("Walk", 0.5f, 0.5f);
            _chains["Run"] = Chain("Run", 1f);
        }

        private static AnimationChainSave Chain(string name, params float[] lengths)
        {
            var chain = new AnimationChainSave { Name = name };
            float x = 0;
            foreach (float length in lengths)
            {
                chain.Frames.Add(new AnimationFrameSave { TextureName = name + ".png", FrameLength = length, LeftCoordinate = x, RightCoordinate = x + 0.25f, BottomCoordinate = 1 });
                x += 0.25f;
            }
            return chain;
        }

        public AnimationChainSave? FindChain(SequenceChainReferenceSave reference) => _chains.GetValueOrDefault(reference.Chain);

        public string? ResolveTexturePath(string source, AnimationFrameSave frame) => frame.TextureName;

        public AnimationEditor.Core.Utilities.FrameLengthUnit UnitOf(SequenceChainReferenceSave reference) => AnimationEditor.Core.Utilities.FrameLengthUnit.Seconds;
    }

    private static SequenceTrackSave ChainTrack(string chain, bool visible = true) =>
        new() { Name = chain, Visible = visible, Chains = { new SequenceChainReferenceSave { Source = "a.achx", Chain = chain } } };

    private static SequenceReferenceSave Reference(string target, float loops = 1, double start = 0) =>
        new() { Sequence = target, Loops = loops, Start = start };

    private static (SequenceReferenceSave Reference, SequenceSave Target, List<SequenceSave> File) Setup(SequenceSave target, float loops = 1)
    {
        var reference = Reference(target.Name, loops);
        var holder = new SequenceSave { Name = "Holder", Tracks = { new SequenceTrackSave { Name = "Link", Reference = reference } } };
        return (reference, target, new List<SequenceSave> { holder, target });
    }

    // At 100 px/s, a 1 s cycle is 100 px: 4 cells of 25 px.
    [Fact]
    public void Cells_CycleIsCutIntoEqualCellsAboutAFilmRowWide_SampledAtTheirCenters()
    {
        var cells = ReferenceFilmstrip.Cells(1, 1, 100);

        cells.Count.ShouldBe(4);
        cells.Select(c => c.Width).ShouldAllBe(w => System.Math.Abs(w - 25) < 1e-9);
        cells.Select(c => c.X).ShouldBe(new[] { 0.0, 25, 50, 75 });
        cells.Select(c => c.Time).ShouldBe(new[] { 0.125, 0.375, 0.625, 0.875 });
    }

    [Fact]
    public void Cells_EachCycleRepeatsTheSameCells()
    {
        var cells = ReferenceFilmstrip.Cells(1, 3, 100);

        cells.Count.ShouldBe(12);
        cells.Select(c => c.Cycle).Distinct().ShouldBe(new[] { 0, 1, 2 });
        cells[4].X.ShouldBe(100, 1e-9);
        cells[4].Time.ShouldBe(1.125, 1e-9);
    }

    [Fact]
    public void Cells_PartialLastCycle_IsCutAtTheClipEnd_AndSampledAtTheMiddleOfWhatShows()
    {
        var cells = ReferenceFilmstrip.Cells(1, 1.5f, 100);

        cells.Count.ShouldBe(6);
        (cells[^1].X + cells[^1].Width).ShouldBe(150, 1e-9);
        cells[^1].Width.ShouldBe(25, 1e-9);
        var cut = ReferenceFilmstrip.Cells(1, 1.4f, 100);
        cut[^1].Width.ShouldBe(15, 1e-4);
        cut[^1].FullWidth.ShouldBe(25, 1e-9);
        cut[^1].Time.ShouldBe(1.325, 1e-4);
    }

    [Theory]
    [InlineData(0.001, 1)]      // a sliver of a cycle is one cell
    [InlineData(10000, 96)]      // zoomed far in the count stops growing and the cells widen
    public void Cells_CountPerCycleIsAtLeastOneAndCapped(double pixelsPerSecond, int expected)
    {
        ReferenceFilmstrip.Cells(1, 1, pixelsPerSecond).Count.ShouldBe(expected);
    }

    [Fact]
    public void Cells_TargetWithNoLength_HasNone()
    {
        ReferenceFilmstrip.Cells(0, 1, 100).ShouldBeEmpty();
    }

    [Fact]
    public void PoseAt_TwoInstantsInTheSameFrame_ArePosesThatAreEqual_AndDifferentFramesAreNot()
    {
        var (reference, target, file) = Setup(new SequenceSave { Name = "T", Tracks = { ChainTrack("Walk") } });
        var lookup = new Lookup();

        var early = ReferenceFilmstrip.PoseAt(reference, target, 0.1, lookup, file)!;
        var alsoFirstFrame = ReferenceFilmstrip.PoseAt(reference, target, 0.4, lookup, file)!;
        var secondFrame = ReferenceFilmstrip.PoseAt(reference, target, 0.6, lookup, file)!;

        early.ShouldBe(alsoFirstFrame);
        early.GetHashCode().ShouldBe(alsoFirstFrame.GetHashCode());
        early.ShouldNotBe(secondFrame);
    }

    [Fact]
    public void PoseAt_LayersTheVisibleTracksBackToFront_AndLeavesOutHiddenOnes()
    {
        var (reference, target, file) = Setup(new SequenceSave
        {
            Name = "T", Tracks = { ChainTrack("Walk"), ChainTrack("Run", visible: false), ChainTrack("Run") },
        });

        var pose = ReferenceFilmstrip.PoseAt(reference, target, 0.1, new Lookup(), file)!;

        pose.Layers.Select(l => l.Texture).ShouldBe(new[] { "Run.png", "Walk.png" });   // the first track is in front
    }

    [Fact]
    public void PoseAt_NestedReferenceInTheTarget_ShowsItsSprites()
    {
        var inner = new SequenceSave { Name = "Inner", Tracks = { ChainTrack("Run") } };
        var (reference, target, file) = Setup(new SequenceSave
        {
            Name = "Outer", Tracks = { new SequenceTrackSave { Name = "Nest", Reference = Reference("Inner") }, ChainTrack("Walk") },
        });
        file.Add(inner);

        var pose = ReferenceFilmstrip.PoseAt(reference, target, 0.1, new Lookup(), file)!;

        pose.Layers.Select(l => l.Texture).ShouldBe(new[] { "Walk.png", "Run.png" });
    }

    [Fact]
    public void PoseAt_NothingShowsOrPastTheClip_IsNull()
    {
        var (reference, target, file) = Setup(new SequenceSave { Name = "T", Tracks = { ChainTrack("Walk", visible: false) } });
        ReferenceFilmstrip.PoseAt(reference, target, 0.1, new Lookup(), file).ShouldBeNull();

        var (shown, shownTarget, shownFile) = Setup(new SequenceSave { Name = "T", Tracks = { ChainTrack("Walk") } });
        ReferenceFilmstrip.PoseAt(shown, shownTarget, 5, new Lookup(), shownFile).ShouldBeNull();
    }

    [Fact]
    public void Cells_FractionalLoops_AreCutAtTheClipEnd_AndZeroLoopsHasNone()
    {
        var half = ReferenceFilmstrip.Cells(targetLength: 1, loops: 0.5f, pixelsPerSecond: 100);

        half.Sum(c => c.Width).ShouldBe(50, 1e-6);
        half.ShouldAllBe(c => c.Cycle == 0);
        ReferenceFilmstrip.Cells(1, 0, 100).ShouldBeEmpty();
    }

    [Fact]
    public void PoseAt_NonLoopingTarget_HoldsItsLastFrameInLaterCycles()
    {
        var (reference, target, file) = Setup(new SequenceSave { Name = "T", Loop = false, Tracks = { ChainTrack("Walk") } }, loops: 3);

        var last = ReferenceFilmstrip.PoseAt(reference, target, 0.9, new Lookup(), file)!;
        var held = ReferenceFilmstrip.PoseAt(reference, target, 2.2, new Lookup(), file)!;

        held.ShouldBe(last);
        ReferenceFilmstrip.PoseAt(reference, target, 0.1, new Lookup(), file).ShouldNotBe(last);
    }
}
