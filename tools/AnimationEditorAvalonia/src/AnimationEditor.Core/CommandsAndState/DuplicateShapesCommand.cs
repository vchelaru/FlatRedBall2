using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Deep-copies multiple shapes on one frame as a single undo step.
/// </summary>
internal sealed class DuplicateShapesCommand : IUndoableCommand
{
    private readonly AnimationFrameSave _frame;
    private readonly object[] _copies;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly List<object> _preSelection;

    public string Description { get; }

    public DuplicateShapesCommand(
        AnimationFrameSave frame,
        IReadOnlyList<object> copies,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
    {
        _frame = frame;
        _copies = copies.ToArray();
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _preSelection = new List<object>(_selectedState.SelectedNodes);
        Description = _copies.Length == 1 ? "Duplicate Shape" : $"Duplicate {_copies.Length} Shapes";
    }

    public bool Do()
    {
        if (_copies.Length == 0) return false;
        _frame.ShapesSave ??= new ShapesSave();
        foreach (var copy in _copies)
            _frame.ShapesSave.Shapes.Add(copy);
        RaiseSideEffects();
        _selectedState.SelectedNodes = _copies.ToList();
        SelectPrimaryShape(_copies[^1]);
        return true;
    }

    public void Undo()
    {
        foreach (var copy in _copies)
            _frame.ShapesSave!.Shapes.Remove(copy);
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
