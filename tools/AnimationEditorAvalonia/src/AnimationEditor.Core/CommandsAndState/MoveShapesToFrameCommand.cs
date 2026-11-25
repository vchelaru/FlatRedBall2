using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Moves the <em>same</em> shape instances, each out of the frame it is in, to the end of their type group in a frame.
/// A shape that comes from another frame and whose name is taken gets a free one.
/// </summary>
internal sealed class MoveShapesToFrameCommand : IUndoableCommand
{
    private readonly IReadOnlyList<ShapeSave> _shapes;
    private readonly AnimationFrameSave[] _frames;
    private readonly AnimationFrameSave _target;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly SelectionBefore _selectionBefore;
    private readonly string[] _oldNames;

    // Captured by Do().
    private bool _createdShapesSave;
    private object[][] _before = [];

    public string Description { get; }

    /// <param name="shapes">The block. A shape that is in none of the frames is added.</param>
    public MoveShapesToFrameCommand(IReadOnlyList<ShapeSave> shapes, IEnumerable<AnimationFrameSave> sources, AnimationFrameSave target,
        IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
    {
        _shapes = shapes;
        _frames = sources.Append(target).Distinct().ToArray();
        _target = target;
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _selectionBefore = new SelectionBefore(selectedState);
        _oldNames = shapes.Select(s => s.Name).ToArray();
        Description = shapes.Count == 1 ? $"Cut {ShapeUndoLabel.Format(shapes[0])}" : $"Cut {shapes.Count} Shapes";
    }

    public bool Do()
    {
        _createdShapesSave = _target.ShapesSave is null;
        _target.ShapesSave ??= new ShapesSave();
        _before = _frames.Select(f => f.ShapesSave!.Shapes.ToArray()).ToArray();

        var names = _target.ShapesSave.Shapes.OfType<ShapeSave>().Select(s => s.Name).ToList();
        foreach (var shape in _shapes.Where(s => _target.ShapesSave.IndexOf(s) < 0))
        {
            shape.Name = StringFunctions.MakeStringUnique(shape.Name, names, 2);
            names.Add(shape.Name);
        }
        foreach (var shape in _shapes)
        {
            foreach (var frame in _frames)
                frame.ShapesSave!.Remove(shape);
            _target.ShapesSave.Add(shape);
        }
        if (_frames.Zip(_before).All(x => x.First.ShapesSave!.Shapes.SequenceEqual(x.Second)))
            return false; // the shapes already were there, so no undo entry

        RaiseSideEffects();
        _selectedState.SelectedNodes = _shapes.Cast<object>().ToList();
        _selectedState.SelectedFrame = _target;
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
        }
        foreach (var (frame, before) in _frames.Zip(_before))
        {
            foreach (var shape in before)
                if (frame.ShapesSave!.IndexOf(shape) < 0)
                    frame.ShapesSave.Add(shape);
            frame.ShapesSave!.SetOrder(before);
        }
        // See AddShapeCommand.Undo: an empty list would still be written to the file.
        if (_createdShapesSave)
            _target.ShapesSave = null;
        RaiseSideEffects();
        _selectionBefore.Restore();
    }

    public void Redo() => Do();

    private void RaiseSideEffects()
    {
        foreach (var frame in _frames)
            _commands.RefreshTreeNode(frame);
        _commands.RefreshAnimationFrameDisplay();
        _events.RaiseAnimationChainsChanged();
    }
}
