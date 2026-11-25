using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>Wraps a chain reorder so the moved chains end up selected, and undo selects what was selected before.</summary>
internal sealed class SelectMovedChainsCommand(
    IUndoableCommand move, IReadOnlyList<AnimationChainSave> chains, ISelectedState selectedState) : IUndoableCommand
{
    private readonly SelectionBefore _selectionBefore = new(selectedState);

    public string Description => move.Description;

    public bool Do()
    {
        bool moved = move.Do();
        SelectChains();
        return moved;
    }

    public void Undo()
    {
        move.Undo();
        _selectionBefore.Restore();
    }

    public void Redo()
    {
        move.Redo();
        SelectChains();
    }

    private void SelectChains()
    {
        selectedState.SelectedNodes = chains.Cast<object>().ToList();
        selectedState.SelectedChain = chains[^1];
    }
}
