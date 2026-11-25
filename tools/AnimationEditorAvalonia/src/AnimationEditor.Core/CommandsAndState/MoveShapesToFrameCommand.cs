using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Moves the <em>same</em> shape instances to the end of their type group in a frame, which may be the one they are
/// already in. A shape whose name is taken in another frame gets a free one.
/// </summary>
internal sealed class MoveShapesToFrameCommand : IUndoableCommand
{
    private readonly IReadOnlyList<ShapeSave> _shapes;
    private readonly AnimationFrameSave _source;
    private readonly AnimationFrameSave _target;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly List<object> _preSelection;
    private readonly string[] _oldNames;

    // Captured by Do().
    private bool _createdShapesSave;
    private object[] _sourceOrder = [];

    public string Description { get; }

    public MoveShapesToFrameCommand(IReadOnlyList<ShapeSave> shapes, AnimationFrameSave source, AnimationFrameSave target,
        IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
    {
        _shapes = shapes;
        _source = source;
        _target = target;
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _preSelection = new List<object>(selectedState.SelectedNodes);
        _oldNames = shapes.Select(s => s.Name).ToArray();
        Description = shapes.Count == 1 ? $"Cut {ShapeUndoLabel.Format(shapes[0])}" : $"Cut {shapes.Count} Shapes";
    }

    public bool Do()
    {
        _createdShapesSave = _target.ShapesSave is null;
        _sourceOrder = _source.ShapesSave!.Shapes.ToArray();

        foreach (var shape in _shapes)
            _source.ShapesSave.Remove(shape);
        _target.ShapesSave ??= new ShapesSave();
        var names = _target.ShapesSave.Shapes.OfType<ShapeSave>().Select(s => s.Name).ToList();
        foreach (var shape in _shapes)
        {
            // Within its own frame a move never renames, even when two shapes there share a name.
            if (_source != _target)
                shape.Name = StringFunctions.MakeStringUnique(shape.Name, names, 2);
            names.Add(shape.Name);
            _target.ShapesSave.Add(shape);
        }
        if (_source == _target && _source.ShapesSave.Shapes.SequenceEqual(_sourceOrder))
            return false; // the shapes already were there, so no undo entry

        RaiseSideEffects();
        _selectedState.SelectedNodes = _shapes.Cast<object>().ToList();
        _selectedState.SelectShape(_shapes[^1]);
        return true;
    }

    public void Undo()
    {
        foreach (var (shape, oldName) in _shapes.Zip(_oldNames))
        {
            _target.ShapesSave!.Remove(shape);
            shape.Name = oldName;
            _source.ShapesSave!.Add(shape);
        }
        _source.ShapesSave!.SetOrder(_sourceOrder);
        // See AddShapeCommand.Undo: an empty list would still be written to the file.
        if (_createdShapesSave)
            _target.ShapesSave = null;
        RaiseSideEffects();
        _selectedState.SelectedNodes = _preSelection;
        _selectedState.SelectShape(_preSelection.FirstOrDefault(n => n is ShapeSave));
    }

    public void Redo() => Do();

    private void RaiseSideEffects()
    {
        _commands.RefreshTreeNode(_source);
        if (_source != _target)
            _commands.RefreshTreeNode(_target);
        _commands.RefreshAnimationFrameDisplay();
        _events.RaiseAnimationChainsChanged();
    }
}
