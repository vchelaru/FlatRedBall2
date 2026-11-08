using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Sequences;
using Shouldly;
using System;
using System.Linq;
using Xunit;
using Cmd = AnimationEditor.Core.Sequences.TimelineMenuCommand;
using Target = AnimationEditor.Core.Sequences.TimelineMenuTarget;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>What the timeline's right-click menus offer, in the ANIMATIONS tree's order, wording and icons.</summary>
public class TimelineMenuLayoutTests
{
    private static Cmd[][] Groups(Target target, bool anyHidden = false) =>
        TimelineMenuLayout.For(target, anyHidden).Select(group => group.ToArray()).ToArray();

    [Fact]
    public void ATrackHeader_OffersCopyCutPasteDuplicate_ThenRenameAndVisibility_ThenDelete() =>
        Groups(Target.TrackHeader).ShouldBe(new[]
        {
            new[] { Cmd.Copy, Cmd.Cut, Cmd.Paste, Cmd.Duplicate },
            new[] { Cmd.RenameTrack, Cmd.ToggleVisibility },
            new[] { Cmd.Delete },
        });

    [Fact]
    public void ATrackHeader_OffersShowAllTracks_OnlyWhileATrackIsHidden() =>
        Groups(Target.TrackHeader, anyHidden: true)[1].ShouldBe(new[] { Cmd.RenameTrack, Cmd.ToggleVisibility, Cmd.ShowAllTracks });

    [Fact]
    public void AChain_OffersCopyCutPasteDuplicate_ThenGoToDefinition_ThenRemove() =>
        Groups(Target.Chain).ShouldBe(new[]
        {
            new[] { Cmd.Copy, Cmd.Cut, Cmd.Paste, Cmd.Duplicate },
            new[] { Cmd.GoToDefinition },
            new[] { Cmd.Delete },
        });

    [Fact]
    public void BlankSpace_OffersAddTrackAndPaste() =>
        Groups(Target.Blank).ShouldBe(new[] { new[] { Cmd.AddTrack }, new[] { Cmd.Paste } });

    [Theory]
    [InlineData(Target.TrackHeader, Cmd.Copy, 1, "Copy")]
    [InlineData(Target.TrackHeader, Cmd.Copy, 3, "Copy 3 Tracks")]
    [InlineData(Target.TrackHeader, Cmd.Cut, 2, "Cut 2 Tracks")]
    [InlineData(Target.TrackHeader, Cmd.Duplicate, 1, "Duplicate")]
    [InlineData(Target.TrackHeader, Cmd.Duplicate, 2, "Duplicate 2 Tracks")]
    [InlineData(Target.TrackHeader, Cmd.Delete, 1, "Delete Track")]
    [InlineData(Target.TrackHeader, Cmd.Delete, 2, "Delete 2 Tracks")]
    [InlineData(Target.Chain, Cmd.Copy, 2, "Copy 2 Chains")]
    [InlineData(Target.Chain, Cmd.Duplicate, 1, "Duplicate")]
    [InlineData(Target.Chain, Cmd.Duplicate, 3, "Duplicate 3 Chains")]
    [InlineData(Target.Chain, Cmd.Delete, 1, "Remove from Track")]
    [InlineData(Target.Chain, Cmd.Delete, 2, "Remove 2 Chains")]
    [InlineData(Target.Blank, Cmd.Paste, 0, "Paste")]
    [InlineData(Target.Blank, Cmd.AddTrack, 0, "Add Track")]
    [InlineData(Target.Chain, Cmd.GoToDefinition, 2, "Go to Definition")]
    public void Header_CountsTheSelection_AsTheTreeMenuDoes(Target target, Cmd command, int selected, string expected) =>
        TimelineMenuLayout.Header(target, command, selected).ShouldBe(expected);

    [Fact]
    public void ToggleVisibility_NamesWhatItWillDo()
    {
        TimelineMenuLayout.Header(Target.TrackHeader, Cmd.ToggleVisibility, 1, trackHidden: false).ShouldBe("Hide Track");
        TimelineMenuLayout.Header(Target.TrackHeader, Cmd.ToggleVisibility, 1, trackHidden: true).ShouldBe("Show Track");
    }

    [Theory]
    [InlineData(Cmd.Copy, TreeMenuIcon.Copy)]
    [InlineData(Cmd.Cut, TreeMenuIcon.Cut)]
    [InlineData(Cmd.Paste, TreeMenuIcon.Paste)]
    [InlineData(Cmd.Duplicate, TreeMenuIcon.Duplicate)]
    [InlineData(Cmd.RenameTrack, TreeMenuIcon.Rename)]
    [InlineData(Cmd.Delete, TreeMenuIcon.Delete)]
    public void ItemsWithATreeCounterpart_UseItsIcon(Cmd command, TreeMenuIcon expected) =>
        TimelineMenuLayout.Icon(command).ShouldBe(expected);

    [Theory]
    [InlineData(Cmd.ToggleVisibility, TreeMenuIcon.Visibility)]
    [InlineData(Cmd.ShowAllTracks, TreeMenuIcon.Visibility)]
    [InlineData(Cmd.GoToDefinition, TreeMenuIcon.Animation)]
    [InlineData(Cmd.AddTrack, TreeMenuIcon.Plus)]
    public void ItemsWithoutATreeCounterpart_UseTheNearestExistingIcon(Cmd command, TreeMenuIcon expected) =>
        TimelineMenuLayout.Icon(command).ShouldBe(expected);

    [Fact]
    public void EveryCommand_HasAnIcon_AsEveryTreeMenuItemDoes()
    {
        foreach (var command in Enum.GetValues<Cmd>())
            TimelineMenuLayout.Icon(command).ShouldNotBeNull($"{command} has no icon");
    }
}
