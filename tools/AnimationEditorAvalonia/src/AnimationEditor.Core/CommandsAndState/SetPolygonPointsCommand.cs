using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Replaces a polygon's whole point list (closing point included) with a snapshot. Every vertex
/// edit goes through this: a drag, an insert, a delete, or a typed coordinate.
/// </summary>
internal sealed class SetPolygonPointsCommand : IUndoableCommand
{
    private readonly AnimationFrameSave? _frame;
    private readonly PolygonSave _polygon;
    private readonly List<Vector2Save> _before;
    private readonly List<Vector2Save> _after;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;

    public string Description { get; }

    /// <summary>Set for typed coordinate edits so one value's keystrokes collapse into one undo entry.</summary>
    public string? CoalesceGroup { get; }

    public SetPolygonPointsCommand(AnimationFrameSave? frame, PolygonSave polygon,
        List<Vector2Save> before, List<Vector2Save> after,
        IAppCommands commands, IApplicationEvents events, string description, string? coalesceGroup = null)
    {
        _frame = frame;
        _polygon = polygon;
        _before = before;
        _after = after;
        _commands = commands;
        _events = events;
        Description = description;
        CoalesceGroup = coalesceGroup;
    }

    public bool Do()
    {
        if (_before.Count == _after.Count
            && _before.Zip(_after).All(p => p.First.X == p.Second.X && p.First.Y == p.Second.Y))
            return false;
        Apply(_after);
        return true;
    }

    public void Undo() => Apply(_before);
    public void Redo() => Apply(_after);

    public IUndoableCommand CoalesceWith(IUndoableCommand previous)
    {
        var p = (SetPolygonPointsCommand)previous;
        return new SetPolygonPointsCommand(_frame, _polygon, p._before, _after,
            _commands, _events, Description, CoalesceGroup);
    }

    private void Apply(List<Vector2Save> points)
    {
        _polygon.Points.Clear();
        foreach (var point in points)
            _polygon.Points.Add(new Vector2Save { X = point.X, Y = point.Y });
        if (_frame is not null) _commands.RefreshTreeNode(_frame);
        _commands.RefreshAnimationFrameDisplay();
        _commands.RefreshWireframe();
        _events.RaiseAnimationChainsChanged();
    }
}
