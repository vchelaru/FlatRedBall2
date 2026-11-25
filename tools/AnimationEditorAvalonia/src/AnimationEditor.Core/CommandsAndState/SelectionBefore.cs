using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>What was selected when a command was made, for its undo.</summary>
internal sealed class SelectionBefore(ISelectedState selectedState)
{
    private readonly List<object> _nodes = new(selectedState.SelectedNodes);
    private readonly AnimationChainSave? _chain = selectedState.SelectedChain;
    private readonly AnimationFrameSave? _frame = selectedState.SelectedFrame;
    private readonly object? _shape = selectedState.SelectedShape;

    public void Restore()
    {
        if (_frame is null) selectedState.SelectedChain = _chain;
        else selectedState.SelectedFrame = _frame;
        selectedState.SelectShape(_shape);
        selectedState.SelectedNodes = _nodes;
    }
}
