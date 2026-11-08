using AnimationEditor.Core.CommandsAndState;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>What a right-click on the timeline hit.</summary>
public enum TimelineMenuTarget
{
    /// <summary>A track's header; the menu acts on the selected tracks.</summary>
    TrackHeader,

    /// <summary>A chain block; the menu acts on the selected chains.</summary>
    Chain,

    /// <summary>Empty lane space, the new-track row or the space below it.</summary>
    Blank,
}

/// <summary>An item in the timeline's right-click menu.</summary>
public enum TimelineMenuCommand
{
    Copy,
    Cut,
    Paste,
    Duplicate,
    RenameTrack,
    ToggleVisibility,
    ShowAllTracks,
    GoToDefinition,
    Delete,
    AddTrack,
}

/// <summary>
/// The commands the timeline's menu offers for what was right-clicked, as groups the view separates with a divider, in
/// the order, wording and icons of the ANIMATIONS tree's menu (Copy, Cut, Paste, Duplicate; then the item's own commands;
/// then Delete). Copy, Cut, Duplicate and Delete count several selected tracks or chains as the tree's menu does.
/// </summary>
public static class TimelineMenuLayout
{
    public static IReadOnlyList<IReadOnlyList<TimelineMenuCommand>> For(TimelineMenuTarget target, bool anyTrackHidden)
    {
        var clipboard = new[] { TimelineMenuCommand.Copy, TimelineMenuCommand.Cut, TimelineMenuCommand.Paste, TimelineMenuCommand.Duplicate };
        return target switch
        {
            TimelineMenuTarget.TrackHeader => new IReadOnlyList<TimelineMenuCommand>[]
            {
                clipboard,
                anyTrackHidden
                    ? new[] { TimelineMenuCommand.RenameTrack, TimelineMenuCommand.ToggleVisibility, TimelineMenuCommand.ShowAllTracks }
                    : new[] { TimelineMenuCommand.RenameTrack, TimelineMenuCommand.ToggleVisibility },
                new[] { TimelineMenuCommand.Delete },
            },
            TimelineMenuTarget.Chain => new IReadOnlyList<TimelineMenuCommand>[]
            {
                clipboard,
                new[] { TimelineMenuCommand.GoToDefinition },
                new[] { TimelineMenuCommand.Delete },
            },
            _ => new IReadOnlyList<TimelineMenuCommand>[]
            {
                new[] { TimelineMenuCommand.AddTrack },
                new[] { TimelineMenuCommand.Paste },
            },
        };
    }

    /// <summary>The item's text with <paramref name="selected"/> tracks or chains selected; <paramref name="trackHidden"/> says whether the clicked track is hidden.</summary>
    public static string Header(TimelineMenuTarget target, TimelineMenuCommand command, int selected, bool trackHidden = false)
    {
        string plural = target == TimelineMenuTarget.Chain ? "Chains" : "Tracks";
        bool several = selected > 1;
        return command switch
        {
            TimelineMenuCommand.Copy => several ? $"Copy {selected} {plural}" : "Copy",
            TimelineMenuCommand.Cut => several ? $"Cut {selected} {plural}" : "Cut",
            TimelineMenuCommand.Paste => "Paste",
            TimelineMenuCommand.Duplicate => target == TimelineMenuTarget.Chain
                ? (several ? $"Duplicate {selected} Chains" : "Duplicate")
                : (several ? $"Duplicate {selected} Tracks" : "Duplicate"),
            TimelineMenuCommand.RenameTrack => "Rename Track…",
            TimelineMenuCommand.ToggleVisibility => trackHidden ? "Show Track" : "Hide Track",
            TimelineMenuCommand.ShowAllTracks => "Show All Tracks",
            TimelineMenuCommand.GoToDefinition => "Go to Definition",
            TimelineMenuCommand.Delete => target == TimelineMenuTarget.Chain
                ? (several ? $"Remove {selected} Chains" : "Remove from Track")
                : (several ? $"Delete {selected} Tracks" : "Delete Track"),
            TimelineMenuCommand.AddTrack => "Add Track",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
        };
    }

    /// <summary>The icon the ANIMATIONS tree's menu gives the matching item, or null for a command the tree has no counterpart of.</summary>
    public static TreeMenuIcon? Icon(TimelineMenuCommand command) => command switch
    {
        TimelineMenuCommand.Copy => TreeMenuIcon.Copy,
        TimelineMenuCommand.Cut => TreeMenuIcon.Cut,
        TimelineMenuCommand.Paste => TreeMenuIcon.Paste,
        TimelineMenuCommand.Duplicate => TreeMenuIcon.Duplicate,
        TimelineMenuCommand.RenameTrack => TreeMenuIcon.Rename,
        TimelineMenuCommand.Delete => TreeMenuIcon.Delete,
        TimelineMenuCommand.AddTrack => TreeMenuIcon.Plus,
        _ => null,
    };
}
