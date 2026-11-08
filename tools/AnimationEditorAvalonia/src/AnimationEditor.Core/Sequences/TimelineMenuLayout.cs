using AnimationEditor.Core.CommandsAndState;
using System;
using System.Collections.Generic;
using System.Linq;

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
    MoveToTop,
    MoveUp,
    MoveDown,
    MoveToBottom,
}

/// <summary>
/// The commands the timeline's menu offers for what was right-clicked, as groups the view separates with a divider, in
/// the order, wording and icons of the ANIMATIONS tree's menu (Copy, Cut, Paste, Duplicate; then the item's own commands;
/// then Delete). Copy, Cut, Duplicate and Delete count several selected tracks or chains as the tree's menu does.
/// </summary>
public static class TimelineMenuLayout
{
    /// <summary>The reorder a Move item performs.</summary>
    public static ReorderMove MoveOf(TimelineMenuCommand command) => command switch
    {
        TimelineMenuCommand.MoveToTop => ReorderMove.ToTop,
        TimelineMenuCommand.MoveUp => ReorderMove.Up,
        TimelineMenuCommand.MoveDown => ReorderMove.Down,
        TimelineMenuCommand.MoveToBottom => ReorderMove.ToBottom,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Not a move item."),
    };

    /// <summary>
    /// The menu's groups. <paramref name="moves"/> are the lane moves that would change something for the selected track
    /// (<see cref="TrackReorder.AvailableMoves"/>), offered first on a track header as the tree menu offers its reorder items;
    /// one that can't happen is left out, and so is the divider under the group when none can.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<TimelineMenuCommand>> For(TimelineMenuTarget target, bool anyTrackHidden, IReadOnlyList<ReorderMove>? moves = null)
    {
        var clipboard = new[] { TimelineMenuCommand.Copy, TimelineMenuCommand.Cut, TimelineMenuCommand.Paste, TimelineMenuCommand.Duplicate };
        var moveItems = new List<TimelineMenuCommand>();
        if (moves is not null)
        {
            if (moves.Contains(ReorderMove.ToTop)) moveItems.Add(TimelineMenuCommand.MoveToTop);
            if (moves.Contains(ReorderMove.Up)) moveItems.Add(TimelineMenuCommand.MoveUp);
            if (moves.Contains(ReorderMove.Down)) moveItems.Add(TimelineMenuCommand.MoveDown);
            if (moves.Contains(ReorderMove.ToBottom)) moveItems.Add(TimelineMenuCommand.MoveToBottom);
        }
        var header = new List<IReadOnlyList<TimelineMenuCommand>>();
        if (moveItems.Count > 0) header.Add(moveItems.ToArray());
        header.Add(clipboard);
        header.Add(anyTrackHidden
            ? new[] { TimelineMenuCommand.RenameTrack, TimelineMenuCommand.ToggleVisibility, TimelineMenuCommand.ShowAllTracks }
            : new[] { TimelineMenuCommand.RenameTrack, TimelineMenuCommand.ToggleVisibility });
        header.Add(new[] { TimelineMenuCommand.Delete });
        return target switch
        {
            TimelineMenuTarget.TrackHeader => header,
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
            TimelineMenuCommand.MoveToTop => "Move To Top",
            TimelineMenuCommand.MoveUp => "Move Up",
            TimelineMenuCommand.MoveDown => "Move Down",
            TimelineMenuCommand.MoveToBottom => "Move To Bottom",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
        };
    }

    /// <summary>The icon the ANIMATIONS tree's menu gives the matching item; the tree has no counterpart of Hide/Show or Go to Definition, so those reuse the nearest existing art.</summary>
    public static TreeMenuIcon? Icon(TimelineMenuCommand command) => command switch
    {
        TimelineMenuCommand.Copy => TreeMenuIcon.Copy,
        TimelineMenuCommand.Cut => TreeMenuIcon.Cut,
        TimelineMenuCommand.Paste => TreeMenuIcon.Paste,
        TimelineMenuCommand.Duplicate => TreeMenuIcon.Duplicate,
        TimelineMenuCommand.RenameTrack => TreeMenuIcon.Rename,
        TimelineMenuCommand.Delete => TreeMenuIcon.Delete,
        TimelineMenuCommand.AddTrack => TreeMenuIcon.Plus,
        TimelineMenuCommand.ToggleVisibility or TimelineMenuCommand.ShowAllTracks => TreeMenuIcon.Visibility,
        TimelineMenuCommand.GoToDefinition => TreeMenuIcon.Animation,
        TimelineMenuCommand.MoveToTop => TreeMenuIcon.MoveToTop,
        TimelineMenuCommand.MoveUp => TreeMenuIcon.MoveUp,
        TimelineMenuCommand.MoveDown => TreeMenuIcon.MoveDown,
        TimelineMenuCommand.MoveToBottom => TreeMenuIcon.MoveToBottom,
        _ => null,
    };
}
