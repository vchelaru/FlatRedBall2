using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.Animation.Content;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Inserts copies of multiple chains as one contiguous block after the last source,
/// matching <see cref="ChainPasteLogic.InsertPastedChains"/> placement.
/// </summary>
internal sealed class DuplicateChainsCommand : IUndoableCommand
{
    private readonly AnimationChainListSave _acls;
    private readonly AnimationChainSave[] _sources;
    private readonly AnimationChainSave[] _copies;
    private readonly int _insertIndex;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly List<object> _preSelection;

    public string Description { get; }

    public DuplicateChainsCommand(
        AnimationChainListSave acls,
        IReadOnlyList<(AnimationChainSave Source, AnimationChainSave Copy)> items,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
    {
        _acls = acls;
        _sources = items.Select(i => i.Source).ToArray();
        _copies = items.Select(i => i.Copy).ToArray();
        _insertIndex = ChainPasteLogic.ResolveBlockInsertIndexAfterAnchors(acls, _sources);
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _preSelection = new List<object>(_selectedState.SelectedNodes);
            Description = _copies.Length == 1
                ? $"Duplicate Animation '{_sources[0].Name}'"
                : $"Duplicate {_copies.Length} Animations";
    }

    public bool Do()
    {
        if (_copies.Length == 0) return false;

        ChainPasteLogic.InsertChainBlockAt(_acls, _copies, _insertIndex);
        RaiseSideEffects();
        _selectedState.SelectedNodes = _copies.Cast<object>().ToList();
        _selectedState.SelectedChain = _copies[^1];
        return true;
    }

    public void Undo()
    {
        foreach (var copy in _copies)
            _acls.AnimationChains.Remove(copy);
        RaiseSideEffects();
        _selectedState.SelectedNodes = _preSelection;
        _selectedState.SelectedChain = _preSelection.OfType<AnimationChainSave>().LastOrDefault();
    }

    public void Redo() => Do();

    private void RaiseSideEffects()
    {
        _commands.RefreshTreeView();
        _events.RaiseAnimationChainsChanged();
        _commands.SaveCurrentAnimationChainList();
    }
}
