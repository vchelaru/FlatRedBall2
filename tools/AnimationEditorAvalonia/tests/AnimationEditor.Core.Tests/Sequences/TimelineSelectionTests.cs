using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The timeline's selection set: chains or tracks, never both; click, Ctrl, Shift and Select All rules.</summary>
public class TimelineSelectionTests
{
    private static TimelineChainId Id(int track, int chain) => new(track, chain);

    private static SequenceChainReferenceSave Ref(double start) => new() { Source = "hero.achx", Chain = "Walk", Start = start };

    // Every chain is half a second long. Track 0 lists its chains out of time order on purpose (starts 2, 0, 1).
    private static IReadOnlyList<TimelineBlock> Blocks()
    {
        var sequence = new SequenceSave();
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { Ref(2), Ref(0), Ref(1) } });
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { Ref(0), Ref(1) } });
        return TimelineLayout.Blocks(sequence, _ => 0.5);
    }

    [Fact]
    public void SelectChain_ReplacesWhateverWasSelected_AndMakesItThePrimary()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 0));
        selection.ToggleChain(Id(0, 1));

        selection.SelectChain(Id(1, 0)).ShouldBeTrue();

        selection.Chains.ShouldBe(new[] { Id(1, 0) });
        selection.PrimaryChain.ShouldBe(Id(1, 0));
        selection.Kind.ShouldBe(TimelineSelectionKind.Chains);
        selection.SelectChain(Id(1, 0)).ShouldBeFalse();
    }

    [Fact]
    public void ToggleChain_AddsAndRemoves_AndKeepsDocumentOrder()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(1, 1));

        selection.ToggleChain(Id(0, 2));
        selection.ToggleChain(Id(0, 0));

        selection.Chains.ShouldBe(new[] { Id(0, 0), Id(0, 2), Id(1, 1) });
        selection.PrimaryChain.ShouldBe(Id(0, 0));
        selection.ToggleChain(Id(0, 0));
        selection.Chains.ShouldBe(new[] { Id(0, 2), Id(1, 1) });
        // Removing the primary hands it to a remaining chain.
        selection.PrimaryChain.ShouldNotBeNull().ShouldBeOneOf(Id(0, 2), Id(1, 1));
    }

    [Fact]
    public void ToggleChain_LastChainOff_EmptiesTheSelection()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 0));

        selection.ToggleChain(Id(0, 0));

        selection.Kind.ShouldBe(TimelineSelectionKind.None);
        selection.PrimaryChain.ShouldBeNull();
    }

    [Fact]
    public void ToggleChain_WhileTracksAreSelected_DropsTheTracks_ChainsAndTracksNeverMix()
    {
        var selection = new TimelineSelection();
        selection.SelectTrack(1);

        selection.ToggleChain(Id(0, 0));

        selection.Tracks.ShouldBeEmpty();
        selection.Chains.ShouldBe(new[] { Id(0, 0) });
    }

    [Fact]
    public void ExtendChainsTo_SelectsTheRangeInTimeOrderOnTheSameTrack()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 1));   // starts at 0

        // List index 0 starts at 2: the range 0..2 s covers all three, listed in file order.
        selection.ExtendChainsTo(Id(0, 0), Blocks());

        selection.Chains.ShouldBe(new[] { Id(0, 0), Id(0, 1), Id(0, 2) });
        selection.PrimaryChain.ShouldBe(Id(0, 1), "the anchor stays where the range started");
    }

    [Fact]
    public void ExtendChainsTo_OnlyCoversWhatLiesBetweenTheAnchorAndTheClick()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 1));   // 0 s

        selection.ExtendChainsTo(Id(0, 2), Blocks());   // 1 s

        selection.Chains.ShouldBe(new[] { Id(0, 1), Id(0, 2) });
    }

    [Fact]
    public void ExtendChainsTo_AnotherTrackOrNoAnchor_SelectsJustTheClickedChain()
    {
        var selection = new TimelineSelection();
        selection.ExtendChainsTo(Id(0, 1), Blocks());
        selection.Chains.ShouldBe(new[] { Id(0, 1) });

        selection.ExtendChainsTo(Id(1, 1), Blocks());

        selection.Chains.ShouldBe(new[] { Id(1, 1) });
    }

    [Fact]
    public void ExtendChainsTo_ARepeatedShiftClick_ReplacesTheRangeFromTheSameAnchor()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 1));
        selection.ExtendChainsTo(Id(0, 0), Blocks());

        selection.ExtendChainsTo(Id(0, 2), Blocks());

        selection.Chains.ShouldBe(new[] { Id(0, 1), Id(0, 2) });
    }

    [Fact]
    public void ExtendTracksTo_SelectsTheTracksBetween_InEitherDirection()
    {
        var selection = new TimelineSelection();
        selection.SelectTrack(3);

        selection.ExtendTracksTo(1);

        selection.Tracks.ShouldBe(new[] { 1, 2, 3 });
        selection.PrimaryTrack.ShouldBe(3);
        selection.Chains.ShouldBeEmpty();
    }

    [Fact]
    public void ToggleTrack_AddsAndRemoves_AndDropsSelectedChains()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 0));

        selection.ToggleTrack(2);
        selection.ToggleTrack(0);

        selection.Chains.ShouldBeEmpty();
        selection.Tracks.ShouldBe(new[] { 0, 2 });
        selection.ToggleTrack(0);
        selection.Tracks.ShouldBe(new[] { 2 });
    }

    [Fact]
    public void SelectAll_WithAChainSelected_TakesTheChainsOfItsTrack_ThenEveryChainOfTheSequence()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(1, 0));

        selection.SelectAll(Blocks(), trackCount: 2);
        selection.Chains.ShouldBe(new[] { Id(1, 0), Id(1, 1) });

        selection.SelectAll(Blocks(), trackCount: 2);
        selection.Chains.Count.ShouldBe(5);
    }

    [Fact]
    public void SelectAll_WithNothingSelected_TakesEveryChain_AndWithATrackSelected_EveryTrack()
    {
        var selection = new TimelineSelection();

        selection.SelectAll(Blocks(), trackCount: 2);
        selection.Chains.Count.ShouldBe(5);

        selection.SelectTrack(0);
        selection.SelectAll(Blocks(), trackCount: 2);
        selection.Tracks.ShouldBe(new[] { 0, 1 });
        selection.Chains.ShouldBeEmpty();
    }

    [Fact]
    public void Clear_EmptiesBoth_AndReportsWhetherAnythingChanged()
    {
        var selection = new TimelineSelection();
        selection.Clear().ShouldBeFalse();
        selection.SelectTrack(1);

        selection.Clear().ShouldBeTrue();

        selection.Kind.ShouldBe(TimelineSelectionKind.None);
        selection.PrimaryTrack.ShouldBeNull();
    }

    [Fact]
    public void Retain_DropsWhatNoLongerExists_AndMovesThePrimaryToWhatIsLeft()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 0));
        selection.ToggleChain(Id(0, 1));
        selection.ToggleChain(Id(0, 2));   // primary

        selection.Retain(id => id.Chain != 2, trackCount: 2).ShouldBeTrue();

        selection.Chains.ShouldBe(new[] { Id(0, 0), Id(0, 1) });
        selection.PrimaryChain.ShouldNotBeNull().ShouldBeOneOf(Id(0, 0), Id(0, 1));
        selection.Retain(_ => true, trackCount: 2).ShouldBeFalse();
    }

    [Fact]
    public void Retain_DropsTracksPastTheEnd()
    {
        var selection = new TimelineSelection();
        selection.SelectTrack(0);
        selection.ToggleTrack(3);

        selection.Retain(_ => true, trackCount: 2);

        selection.Tracks.ShouldBe(new[] { 0 });
    }

    [Fact]
    public void RemapAfterRemovingChains_ShiftsTheIndicesOfWhatStaysPastTheRemovedOnes()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 3));
        selection.ToggleChain(Id(1, 2));

        selection.RemapAfterRemovingChains(new[] { Id(0, 0), Id(0, 2) });

        selection.Chains.ShouldBe(new[] { Id(0, 1), Id(1, 2) });
    }

    [Fact]
    public void Primary_OfAMultiSelection_IsWhatTheInspectorAndHistoryUse()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 0));
        selection.ToggleChain(Id(1, 1));

        selection.PrimaryChain.ShouldBe(Id(1, 1));
        selection.IsMulti.ShouldBeTrue();
        selection.Count.ShouldBe(2);
    }

    [Fact]
    public void MakePrimary_OfASelectedChain_ChangesOnlyThePrimary()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 0));
        selection.ToggleChain(Id(0, 1));

        selection.MakePrimary(Id(0, 0)).ShouldBeTrue();

        selection.PrimaryChain.ShouldBe(Id(0, 0));
        selection.Chains.ShouldBe(new[] { Id(0, 0), Id(0, 1) });
        selection.MakePrimary(Id(1, 1)).ShouldBeFalse("a chain outside the selection cannot become its primary");
    }

    [Fact]
    public void SelectChains_ReplacesTheSelectionWithTheGivenSet_AndAnEmptySetClears()
    {
        var selection = new TimelineSelection();
        selection.SelectTrack(1);

        selection.SelectChains(new[] { Id(1, 1), Id(0, 2), Id(0, 2) }, primary: Id(1, 1)).ShouldBeTrue();

        selection.Chains.ShouldBe(new[] { Id(0, 2), Id(1, 1) });
        selection.PrimaryChain.ShouldBe(Id(1, 1));
        selection.Tracks.ShouldBeEmpty();
        selection.SelectChains(System.Array.Empty<TimelineChainId>());
        selection.Kind.ShouldBe(TimelineSelectionKind.None);
    }

    [Fact]
    public void SelectTracks_ReplacesTheSelectionWithTheGivenTracks()
    {
        var selection = new TimelineSelection();
        selection.SelectChain(Id(0, 0));

        selection.SelectTracks(new[] { 3, 1 });

        selection.Tracks.ShouldBe(new[] { 1, 3 });
        selection.Chains.ShouldBeEmpty();
    }
}
