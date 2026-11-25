using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Moves the <em>same</em> frame instances (not clones) from one or more source chains to a target
/// chain at a resolved insert index, as one undo step. Covers both within-chain reorder
/// (source == target) and cross-animation moves. The frames become a contiguous, gap-squashed
/// block at the destination, in the order given. Preserving instance identity is what lets
/// the tree's frame VMs survive the move (see <c>TreeBuilder.SyncFramesInto</c>).
/// </summary>
internal sealed class MoveFramesCommand : IUndoableCommand
{
    private readonly IReadOnlyList<AnimationFrameSave> _frames;
    private readonly AnimationChainSave[] _chains;
    private readonly AnimationChainSave _targetChain;
    private readonly int _insertIndex;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly SelectionBefore _selectionBefore;

    private AnimationFrameSave[][] _before = [];

    public string Description { get; }

    /// <summary>Moves frames of one chain, keeping their order there.</summary>
    public MoveFramesCommand(
        IReadOnlyList<AnimationFrameSave> frames,
        AnimationChainSave sourceChain,
        AnimationChainSave targetChain,
        int insertIndex,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
        : this(InOrder(frames, sourceChain),
            [sourceChain], targetChain, insertIndex,
            (frames.Count, ReferenceEquals(sourceChain, targetChain)) switch
            {
                (1, true) => $"Move Frame in '{sourceChain.Name}'",
                (_, true) => $"Move {frames.Count} Frames",
                (1, false) => $"Move Frame to '{targetChain.Name}'",
                _ => $"Move {frames.Count} Frames to '{targetChain.Name}'",
            },
            commands, events, selectedState)
    {
    }

    /// <summary>The frames of <paramref name="chain"/> among <paramref name="frames"/>, in their order there.</summary>
    public static AnimationFrameSave[] InOrder(IReadOnlyList<AnimationFrameSave> frames, AnimationChainSave chain) =>
        frames.Where(chain.Frames.Contains).OrderBy(f => chain.Frames.IndexOf(f)).ToArray();

    /// <param name="frames">The block. A frame that is in none of the chains is added.</param>
    /// <param name="insertIndex">Where the block lands in the target chain, interpreted
    /// against the target's current frame list (before any source removal). For a same-chain
    /// move the index is adjusted internally for the frames removed ahead of it.</param>
    public MoveFramesCommand(
        IReadOnlyList<AnimationFrameSave> frames,
        IReadOnlyList<AnimationChainSave> sourceChains,
        AnimationChainSave targetChain,
        int insertIndex,
        string description,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
    {
        _frames = frames;
        _chains = sourceChains.Append(targetChain).Distinct().ToArray();
        _targetChain = targetChain;
        _insertIndex = insertIndex;
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _selectionBefore = new SelectionBefore(selectedState);
        Description = description;
    }

    public bool Do()
    {
        if (_frames.Count == 0) return false;
        _before = _chains.Select(c => c.Frames.ToArray()).ToArray();

        Arrange(_frames, _chains, _targetChain, _insertIndex);

        if (_chains.Zip(_before).All(x => x.First.Frames.SequenceEqual(x.Second)))
            return false; // the frames already were there — no undo entry

        RaiseSideEffects();
        SelectMoved();
        return true;
    }

    internal static void Arrange(
        IReadOnlyList<AnimationFrameSave> frames, IReadOnlyList<AnimationChainSave> chains, AnimationChainSave targetChain, int insertIndex)
    {
        // Removing the block from the target shifts the insert position left by however many of its frames sat ahead of it.
        int insertAt = insertIndex - targetChain.Frames.Take(insertIndex).Count(frames.Contains);
        foreach (var chain in chains)
            chain.Frames.RemoveAll(frames.Contains);

        insertAt = Math.Clamp(insertAt, 0, targetChain.Frames.Count);
        targetChain.Frames.InsertRange(insertAt, frames);
    }

    public void Undo()
    {
        foreach (var (chain, before) in _chains.Zip(_before))
            RestoreOrder(chain, before);
        RaiseSideEffects();
        _selectionBefore.Restore();
    }

    public void Redo() => Do();

    internal static void RestoreOrder(AnimationChainSave chain, AnimationFrameSave[] order)
    {
        chain.Frames.Clear();
        foreach (var frame in order)
            chain.Frames.Add(frame);
    }

    private void SelectMoved()
    {
        _selectedState.SelectedNodes = _frames.Cast<object>().ToList();
        _selectedState.SelectedFrame = _frames[^1];
    }

    private void RaiseSideEffects()
    {
        foreach (var chain in _chains)
            _commands.RefreshTreeNode(chain);
        _commands.RefreshWireframe();
        _events.RaiseAnimationChainsChanged();
    }
}
