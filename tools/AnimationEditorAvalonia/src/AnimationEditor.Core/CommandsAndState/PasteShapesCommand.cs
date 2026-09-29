using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Undo/redo record for pasting shapes onto one or more frames in a single user action.
/// Selects every pasted shape across all frames, so a multi-frame paste multi-selects the results.
/// </summary>
internal sealed class PasteShapesCommand : IUndoableCommand
{
    private readonly (AnimationFrameSave Frame, object[] Shapes)[] _groups;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly List<object> _preSelection;

    public string Description { get; }

    public PasteShapesCommand(
        AnimationFrameSave frame,
        IReadOnlyList<object> shapes,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
        : this(new[] { (frame, (IReadOnlyList<object>)shapes) }, null, commands, events, selectedState)
    {
    }

    /// <param name="description">Undo label; defaults to "Paste {shape}" / "Paste N Shapes".</param>
    public PasteShapesCommand(
        IReadOnlyList<(AnimationFrameSave Frame, IReadOnlyList<object> Shapes)> groups,
        string? description,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
    {
        _groups = groups.Select(g => (g.Frame, g.Shapes.ToArray())).ToArray();
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _preSelection = new List<object>(_selectedState.SelectedNodes);
        var all = AllShapes();
        Description = description ?? (all.Count == 1
            ? $"Paste {ShapeUndoLabel.Format(all[0])}"
            : $"Paste {all.Count} Shapes");
    }

    private List<object> AllShapes() => _groups.SelectMany(g => g.Shapes).ToList();

    public bool Do()
    {
        var all = AllShapes();
        if (all.Count == 0) return false;
        foreach (var (frame, shapes) in _groups)
        {
            frame.ShapesSave ??= new ShapesSave();
            foreach (var shape in shapes)
                frame.ShapesSave.Shapes.Add(shape);
        }
        RaiseSideEffects();
        _selectedState.SelectedNodes = all;
        _selectedState.SelectShape(all[^1]);
        return true;
    }

    public void Undo()
    {
        foreach (var (frame, shapes) in _groups)
            foreach (var shape in shapes)
                frame.ShapesSave!.Shapes.Remove(shape);
        RaiseSideEffects();
        _selectedState.SelectedNodes = _preSelection;
        _selectedState.SelectShape(_preSelection.FirstOrDefault(n => n is ShapeSave));
    }

    public void Redo() => Do();

    private void RaiseSideEffects()
    {
        foreach (var (frame, _) in _groups)
            _commands.RefreshTreeNode(frame);
        _commands.RefreshAnimationFrameDisplay();
        _events.RaiseAnimationChainsChanged();
    }
}
