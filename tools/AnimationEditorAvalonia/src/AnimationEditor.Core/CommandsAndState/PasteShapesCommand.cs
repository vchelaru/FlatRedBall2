using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Undo/redo record for pasting multiple shapes onto one frame in a single user action.
/// </summary>
internal sealed class PasteShapesCommand : IUndoableCommand
{
    private readonly AnimationFrameSave _frame;
    private readonly object[] _shapes;
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
    {
        _frame = frame;
        _shapes = shapes.ToArray();
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _preSelection = new List<object>(_selectedState.SelectedNodes);
        Description = _shapes.Length == 1 ? "Paste Shape" : $"Paste {_shapes.Length} Shapes";
    }

    public bool Do()
    {
        if (_shapes.Length == 0) return false;
        _frame.ShapesSave ??= new ShapesSave();
        foreach (var shape in _shapes)
            _frame.ShapesSave.Shapes.Add(shape);
        RaiseSideEffects();
        _selectedState.SelectedNodes = _shapes.ToList();
        SelectPrimaryShape(_shapes[^1]);
        return true;
    }

    public void Undo()
    {
        foreach (var shape in _shapes)
            _frame.ShapesSave!.Shapes.Remove(shape);
        RaiseSideEffects();
        _selectedState.SelectedNodes = _preSelection;
        RestorePrimarySelection(_preSelection);
    }

    public void Redo() => Do();

    private void RaiseSideEffects()
    {
        _commands.RefreshTreeNode(_frame);
        _commands.RefreshAnimationFrameDisplay();
        _events.RaiseAnimationChainsChanged();
        _commands.SaveCurrentAnimationChainList();
    }

    private void SelectPrimaryShape(object shape)
    {
        switch (shape)
        {
            case AARectSave r:
                _selectedState.SelectedRectangle = r;
                break;
            case CircleSave c:
                _selectedState.SelectedCircle = c;
                break;
        }
    }

    private void RestorePrimarySelection(List<object> nodes)
    {
        _selectedState.SelectedRectangle = null;
        _selectedState.SelectedCircle = null;
        foreach (var node in nodes)
        {
            switch (node)
            {
                case AARectSave r:
                    _selectedState.SelectedRectangle = r;
                    return;
                case CircleSave c:
                    _selectedState.SelectedCircle = c;
                    return;
            }
        }
    }
}
