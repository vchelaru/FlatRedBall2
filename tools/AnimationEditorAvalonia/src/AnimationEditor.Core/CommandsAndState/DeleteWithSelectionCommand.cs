using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Wraps a delete so the multi-selection bag follows it: deleted shapes leave
    /// <see cref="ISelectedState.SelectedNodes"/> on Do/Redo, and Undo puts the whole selection
    /// back (first shape primary again). Per-item commands only know their own shape, so without
    /// this a multi-delete left dead shapes selected and Undo re-selected just one of them (#1824).
    /// </summary>
    internal sealed class DeleteWithSelectionCommand : IUndoableCommand
    {
        private readonly IUndoableCommand _inner;
        private readonly IReadOnlyList<ShapeSave> _deletedShapes;
        private readonly ISelectedState _selectedState;
        private List<object> _selectionBefore = [];

        public string Description => _inner.Description;

        public DeleteWithSelectionCommand(
            IUndoableCommand inner, IReadOnlyList<ShapeSave> deletedShapes, ISelectedState selectedState)
        {
            _inner = inner;
            _deletedShapes = deletedShapes;
            _selectedState = selectedState;
        }

        public bool Do()
        {
            _selectionBefore = _selectedState.SelectedNodes.ToList();
            if (!_inner.Do()) return false;
            DropDeletedShapesFromSelection();
            return true;
        }

        public void Undo()
        {
            _inner.Undo();
            _selectedState.SelectedNodes = _selectionBefore.ToList();
            if (_selectionBefore.OfType<ShapeSave>().FirstOrDefault() is { } primary)
                _selectedState.SelectShape(primary);
        }

        public void Redo()
        {
            _inner.Redo();
            DropDeletedShapesFromSelection();
        }

        private void DropDeletedShapesFromSelection()
        {
            var deleted = _deletedShapes.ToHashSet();
            var nodes = _selectedState.SelectedNodes;
            if (nodes.Any(n => n is ShapeSave s && deleted.Contains(s)))
                _selectedState.SelectedNodes = nodes.Where(n => n is not ShapeSave s || !deleted.Contains(s)).ToList();
        }
    }
}
