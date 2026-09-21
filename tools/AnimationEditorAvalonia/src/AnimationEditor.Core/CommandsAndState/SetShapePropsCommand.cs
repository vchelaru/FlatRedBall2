using FlatRedBall2.AnimationEditorCommon;
using System.Runtime.CompilerServices;

namespace AnimationEditor.Core.CommandsAndState.Commands;

internal sealed class SetShapePropsCommand : IUndoableCommand
{
    private readonly AnimationFrameSave? _frame;
    private readonly object _shape;
    private readonly string _oldName, _newName;
    private readonly float _oldX, _oldY, _oldP1, _oldP2;
    private readonly float _newX, _newY, _newP1, _newP2;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;

    public string Description { get; }

    /// <summary>
    /// Keyed on the edited shape's own identity, so consecutive edits to the same rect/circle
    /// (e.g. one call per keystroke while typing in the X field) coalesce into a single undo entry
    /// -- see <see cref="IUndoableCommand.CoalesceGroup"/>. Edits to a different shape never share
    /// a key, since <see cref="RuntimeHelpers.GetHashCode"/> is per-instance.
    /// </summary>
    public string? CoalesceGroup { get; }

    public static SetShapePropsCommand ForRect(
        AnimationFrameSave? frame, AARectSave rect,
        string newName, float newX, float newY, float newScaleX, float newScaleY,
        IAppCommands commands, IApplicationEvents events) =>
        new(frame, rect, rect.Name ?? "", newName,
            rect.X, rect.Y, rect.ScaleX, rect.ScaleY,
            newX, newY, newScaleX, newScaleY,
            commands, events, ShapeLabel("Rect", rect.Name ?? ""));

    public static SetShapePropsCommand ForCircle(
        AnimationFrameSave? frame, CircleSave circ,
        string newName, float newX, float newY, float newRadius,
        IAppCommands commands, IApplicationEvents events) =>
        new(frame, circ, circ.Name ?? "", newName,
            circ.X, circ.Y, circ.Radius, 0f,
            newX, newY, newRadius, 0f,
            commands, events, ShapeLabel("Circle", circ.Name ?? ""));

    private static string ShapeLabel(string type, string name) =>
        string.IsNullOrEmpty(name) ? $"Edit {type}" : $"Edit {type} '{name}'";

    private SetShapePropsCommand(
        AnimationFrameSave? frame, object shape,
        string oldName, string newName,
        float oldX, float oldY, float oldP1, float oldP2,
        float newX, float newY, float newP1, float newP2,
        IAppCommands commands, IApplicationEvents events, string description)
    {
        _frame = frame; _shape = shape;
        _oldName = oldName; _newName = newName;
        _oldX = oldX; _oldY = oldY; _oldP1 = oldP1; _oldP2 = oldP2;
        _newX = newX; _newY = newY; _newP1 = newP1; _newP2 = newP2;
        _commands = commands; _events = events;
        Description = description;
        CoalesceGroup = $"ShapeProps:{RuntimeHelpers.GetHashCode(shape)}";
    }

    public bool Do()
    {
        if (_oldName == _newName && _oldX == _newX && _oldY == _newY &&
            _oldP1 == _newP1 && _oldP2 == _newP2) return false;
        Apply(_newName, _newX, _newY, _newP1, _newP2);
        return true;
    }

    public void Undo() => Apply(_oldName, _oldX, _oldY, _oldP1, _oldP2);
    public void Redo() => Apply(_newName, _newX, _newY, _newP1, _newP2);

    /// <summary>
    /// Combines <paramref name="previous"/>'s original "before" state with this command's
    /// already-applied "after" state, so undoing the merged entry restores the value from before
    /// the whole coalesced edit (see <see cref="IUndoableCommand.CoalesceWith"/>).
    /// </summary>
    public IUndoableCommand CoalesceWith(IUndoableCommand previous)
    {
        var p = (SetShapePropsCommand)previous;
        return new SetShapePropsCommand(
            _frame, _shape, p._oldName, _newName,
            p._oldX, p._oldY, p._oldP1, p._oldP2,
            _newX, _newY, _newP1, _newP2,
            _commands, _events, Description);
    }

    private void Apply(string name, float x, float y, float p1, float p2)
    {
        if (_shape is AARectSave r) { r.Name = name; r.X = x; r.Y = y; r.ScaleX = p1; r.ScaleY = p2; }
        else if (_shape is CircleSave c) { c.Name = name; c.X = x; c.Y = y; c.Radius = p1; }
        if (_frame is not null) _commands.RefreshTreeNode(_frame);
        _commands.RefreshAnimationFrameDisplay();
        _commands.RefreshWireframe();
        _events.RaiseAnimationChainsChanged();
    }
}
