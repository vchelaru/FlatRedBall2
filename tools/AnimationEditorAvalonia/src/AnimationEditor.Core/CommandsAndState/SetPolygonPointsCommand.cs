using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Replaces one or more polygons' whole point lists (closing point included) with snapshots.
/// Every vertex edit goes through this: a drag, an insert, a delete, or a typed coordinate, on
/// one polygon or propagated across a multi-selection.
/// </summary>
internal sealed class SetPolygonPointsCommand : IUndoableCommand
{
    /// <summary>One polygon's before/after point lists.</summary>
    public sealed record Entry(AnimationFrameSave? Frame, PolygonSave Polygon, List<Vector2Save> Before, List<Vector2Save> After);

    private readonly IReadOnlyList<Entry> _entries;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;

    public string Description { get; }

    /// <summary>Set for typed coordinate edits so one value's keystrokes collapse into one undo entry.</summary>
    public string? CoalesceGroup { get; }

    public SetPolygonPointsCommand(IReadOnlyList<Entry> entries,
        IAppCommands commands, IApplicationEvents events, string description, string? coalesceGroup = null)
    {
        _entries = entries;
        _commands = commands;
        _events = events;
        Description = description;
        CoalesceGroup = coalesceGroup;
    }

    public bool Do()
    {
        if (_entries.All(e => SamePoints(e.Before, e.After)))
            return false;
        Apply(e => e.After);
        return true;
    }

    public void Undo() => Apply(e => e.Before);
    public void Redo() => Apply(e => e.After);

    public IUndoableCommand CoalesceWith(IUndoableCommand previous)
    {
        // Keep each polygon's earliest "before", so undo restores the state from before the whole
        // coalesced edit. A polygon only the latest keystroke touched keeps its own "before".
        var p = (SetPolygonPointsCommand)previous;
        var entries = _entries
            .Select(e => p._entries.FirstOrDefault(pe => ReferenceEquals(pe.Polygon, e.Polygon)) is { } pe
                ? e with { Before = pe.Before }
                : e)
            .Concat(p._entries.Where(pe => !_entries.Any(e => ReferenceEquals(e.Polygon, pe.Polygon))))
            .ToList();
        return new SetPolygonPointsCommand(entries, _commands, _events, Description, CoalesceGroup);
    }

    private static bool SamePoints(List<Vector2Save> a, List<Vector2Save> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.X == p.Second.X && p.First.Y == p.Second.Y);

    private void Apply(System.Func<Entry, List<Vector2Save>> pick)
    {
        foreach (var entry in _entries)
        {
            entry.Polygon.Points.Clear();
            foreach (var point in pick(entry))
                entry.Polygon.Points.Add(new Vector2Save { X = point.X, Y = point.Y });
            if (entry.Frame is not null) _commands.RefreshTreeNode(entry.Frame);
        }
        _commands.RefreshAnimationFrameDisplay();
        _commands.RefreshWireframe();
        _events.RaiseAnimationChainsChanged();
    }
}
