using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class HiddenTrackSetTests
{
    private static SequenceListSave Save(params (string Sequence, string[] Tracks)[] sequences)
    {
        var save = new SequenceListSave();
        foreach (var (name, tracks) in sequences)
        {
            var sequence = new SequenceSave { Name = name };
            sequence.Tracks.AddRange(tracks.Select(track => new SequenceTrackSave { Name = track }));
            save.Sequences.Add(sequence);
        }
        return save;
    }

    [Fact]
    public void Toggle_HidesThenShows_AndRaisesChangedEachTime()
    {
        var set = new HiddenTrackSet();
        int changes = 0;
        set.Changed += () => changes++;

        set.Toggle("Action", "Moon");
        bool afterFirst = set.IsHidden("Action", "Moon");
        set.Toggle("Action", "Moon");

        afterFirst.ShouldBeTrue();
        set.IsHidden("Action", "Moon").ShouldBeFalse();
        changes.ShouldBe(2);
    }

    [Fact]
    public void IsHidden_IsPerSequence_SoTheSameTrackNameElsewhereStaysShown()
    {
        var set = new HiddenTrackSet();

        set.Toggle("Action", "Moon");

        set.IsHidden("Idle", "Moon").ShouldBeFalse();
        set.AnyHidden("Action").ShouldBeTrue();
        set.AnyHidden("Idle").ShouldBeFalse();
    }

    [Fact]
    public void ShowAll_ClearsOnlyThatSequence()
    {
        var set = new HiddenTrackSet();
        set.Toggle("Action", "Moon");
        set.Toggle("Action", "BlackMoon");
        set.Toggle("Idle", "Moon");

        set.ShowAll("Action");

        set.AnyHidden("Action").ShouldBeFalse();
        set.IsHidden("Idle", "Moon").ShouldBeTrue();
    }

    [Fact]
    public void RenameTrack_CarriesTheHiddenState_ToTheNewName()
    {
        var set = new HiddenTrackSet();
        set.Toggle("Action", "Moon");
        set.Toggle("Action", "Sun");
        int changes = 0;
        set.Changed += () => changes++;

        set.RenameTrack("Action", "Moon", "WhiteMoon");

        set.IsHidden("Action", "WhiteMoon").ShouldBeTrue();
        set.IsHidden("Action", "Moon").ShouldBeFalse();
        set.IsHidden("Action", "Sun").ShouldBeTrue();
        changes.ShouldBe(1);
    }

    [Fact]
    public void RenameTrack_OfAShownTrack_ChangesNothing()
    {
        var set = new HiddenTrackSet();
        int changes = 0;
        set.Changed += () => changes++;

        set.RenameTrack("Action", "Moon", "WhiteMoon");

        set.Entries.ShouldBeEmpty();
        changes.ShouldBe(0);
    }

    [Fact]
    public void RenameSequence_CarriesEveryHiddenTrackOfIt()
    {
        var set = new HiddenTrackSet();
        set.Toggle("Action", "Moon");
        set.Toggle("Action", "BlackMoon");
        set.Toggle("Idle", "Moon");

        set.RenameSequence("Action", "Attack");

        set.IsHidden("Attack", "Moon").ShouldBeTrue();
        set.IsHidden("Attack", "BlackMoon").ShouldBeTrue();
        set.IsHidden("Action", "Moon").ShouldBeFalse();
        set.IsHidden("Idle", "Moon").ShouldBeTrue();
    }

    [Fact]
    public void Prune_DropsTracksAndSequencesThatAreGone_AndRaisesChangedOnlyWhenItDropped()
    {
        var set = new HiddenTrackSet();
        set.Toggle("Action", "Moon");
        set.Toggle("Action", "Deleted");
        set.Toggle("Removed", "Moon");
        int changes = 0;
        set.Changed += () => changes++;
        var save = Save(("Action", new[] { "Moon", "Sun" }), ("Idle", new[] { "Moon" }));

        set.Prune(save);
        set.Prune(save);

        set.Entries.Select(e => (e.Sequence, e.Track)).ShouldBe(new[] { ("Action", "Moon") });
        changes.ShouldBe(1);
    }

    [Fact]
    public void Load_ReplacesTheEntries_WithoutRaisingChanged()
    {
        var set = new HiddenTrackSet();
        set.Toggle("Old", "Track");
        int changes = 0;
        set.Changed += () => changes++;

        set.Load(new[] { new HiddenTrackEntry { Sequence = "Action", Track = "Moon" } });

        set.IsHidden("Action", "Moon").ShouldBeTrue();
        set.IsHidden("Old", "Track").ShouldBeFalse();
        set.Entries.Select(e => (e.Sequence, e.Track)).ShouldBe(new[] { ("Action", "Moon") });
        changes.ShouldBe(0);
    }
}
