using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Moves the <em>same</em> frame instances (not clones) from a source chain to a target
/// chain at a resolved insert index, as one undo step. Covers both within-chain reorder
/// (source == target) and cross-animation moves. The moved frames are sorted by their
/// source-chain index and become a contiguous, gap-squashed block at the destination, so
/// a multi-select drag preserves relative order. Preserving instance identity is what lets
/// the tree's frame VMs survive the move (see <c>TreeBuilder.SyncFramesInto</c>).
/// </summary>
internal sealed class MoveFramesCommand : IUndoableCommand
{
    private readonly IReadOnlyList<AnimationFrameSave> _frames;
    private readonly AnimationChainSave _sourceChain;
    private readonly AnimationChainSave _targetChain;
    private readonly int _insertIndex;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly List<object> _preSelection;

    private AnimationFrameSave[] _moved = [];
    private AnimationFrameSave[] _sourceBefore = [];
    private AnimationFrameSave[] _targetBefore = [];

    public string Description { get; }

    /// <param name="insertIndex">Where the block lands in the target chain, interpreted
    /// against the target's current frame list (before any source removal). For a same-chain
    /// move the index is adjusted internally for the frames removed ahead of it.</param>
    public MoveFramesCommand(
        IReadOnlyList<AnimationFrameSave> frames,
        AnimationChainSave sourceChain,
        AnimationChainSave targetChain,
        int insertIndex,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
    {
        _frames = frames;
        _sourceChain = sourceChain;
        _targetChain = targetChain;
        _insertIndex = insertIndex;
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _preSelection = new List<object>(selectedState.SelectedNodes);
        bool sameChain = ReferenceEquals(sourceChain, targetChain);
        Description = (frames.Count, sameChain) switch
        {
            (1, true) => $"Move Frame in '{sourceChain.Name}'",
            (_, true) => $"Move {frames.Count} Frames",
            (1, false) => $"Move Frame to '{targetChain.Name}'",
            _ => $"Move {frames.Count} Frames to '{targetChain.Name}'",
        };
    }

    public bool Do()
    {
        _sourceBefore = _sourceChain.Frames.ToArray();
        _targetBefore = _targetChain.Frames.ToArray();

        var movedIndices = _frames
            .Select(f => _sourceChain.Frames.IndexOf(f))
            .Where(i => i >= 0)
            .OrderBy(i => i)
            .ToArray();
        if (movedIndices.Length == 0) return false;

        _moved = movedIndices.Select(i => _sourceChain.Frames[i]).ToArray();

        bool sameChain = ReferenceEquals(_sourceChain, _targetChain);

        // Removing the selected frames from the target shifts the insert position left by
        // however many of them sat ahead of it. Cross-chain moves don't touch the target.
        int insertAt = _insertIndex;
        if (sameChain)
            insertAt -= movedIndices.Count(i => i < _insertIndex);

        foreach (var frame in _moved)
            _sourceChain.Frames.Remove(frame);

        insertAt = Math.Clamp(insertAt, 0, _targetChain.Frames.Count);
        for (int i = 0; i < _moved.Length; i++)
            _targetChain.Frames.Insert(insertAt + i, _moved[i]);

        if (sameChain && _targetChain.Frames.SequenceEqual(_targetBefore))
            return false; // reorder produced an identical list — no undo entry

        RaiseSideEffects();
        SelectMoved();
        return true;
    }

    public void Undo()
    {
        RestoreOrder(_sourceChain, _sourceBefore);
        if (!ReferenceEquals(_sourceChain, _targetChain))
            RestoreOrder(_targetChain, _targetBefore);
        RaiseSideEffects();
        _selectedState.SelectedNodes = _preSelection;
        _selectedState.SelectedFrame = _preSelection.OfType<AnimationFrameSave>().LastOrDefault();
    }

    public void Redo() => Do();

    private static void RestoreOrder(AnimationChainSave chain, AnimationFrameSave[] order)
    {
        chain.Frames.Clear();
        foreach (var frame in order)
            chain.Frames.Add(frame);
    }

    private void SelectMoved()
    {
        _selectedState.SelectedNodes = _moved.Cast<object>().ToList();
        _selectedState.SelectedFrame = _moved[^1];
    }

    private void RaiseSideEffects()
    {
        _commands.RefreshTreeNode(_sourceChain);
        if (!ReferenceEquals(_sourceChain, _targetChain))
            _commands.RefreshTreeNode(_targetChain);
        _commands.RefreshWireframe();
        _events.RaiseAnimationChainsChanged();
        _commands.SaveCurrentAnimationChainList();
    }
}
