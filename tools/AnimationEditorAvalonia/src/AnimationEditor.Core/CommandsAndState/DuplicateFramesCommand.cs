using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Linq;

namespace AnimationEditor.Core.CommandsAndState.Commands;

internal abstract class DuplicateFrameInsertOp;

internal sealed class DuplicateFrameBlockOp(
    AnimationChainSave chain,
    AnimationFrameSave[] copies,
    int insertIndex) : DuplicateFrameInsertOp
{
    public AnimationChainSave Chain { get; } = chain;
    public AnimationFrameSave[] Copies { get; } = copies;
    public int InsertIndex { get; } = insertIndex;
}

internal sealed class DuplicateFrameAdjacentOp(
    AnimationChainSave chain,
    AnimationFrameSave source,
    AnimationFrameSave copy) : DuplicateFrameInsertOp
{
    public AnimationChainSave Chain { get; } = chain;
    public AnimationFrameSave Source { get; } = source;
    public AnimationFrameSave Copy { get; } = copy;
}

/// <summary>
/// Inserts frame copies as one undo step. Contiguous same-chain selections become a
/// single block after the range; non-contiguous selections duplicate adjacent to each source.
/// </summary>
internal sealed class DuplicateFramesCommand : IUndoableCommand
{
    private readonly DuplicateFrameInsertOp[] _ops;
    private readonly AnimationFrameSave[] _allCopies;
    private readonly IAppCommands _commands;
    private readonly IApplicationEvents _events;
    private readonly ISelectedState _selectedState;
    private readonly List<object> _preSelection;

    public string Description { get; }

    public DuplicateFramesCommand(
        IReadOnlyList<DuplicateFrameInsertOp> ops,
        IAppCommands commands,
        IApplicationEvents events,
        ISelectedState selectedState)
    {
        _ops = ops.ToArray();
        _allCopies = ops
            .SelectMany(op => op switch
            {
                DuplicateFrameBlockOp b => b.Copies,
                DuplicateFrameAdjacentOp a => new[] { a.Copy },
                _ => [],
            })
            .ToArray();
        _commands = commands;
        _events = events;
        _selectedState = selectedState;
        _preSelection = new List<object>(_selectedState.SelectedNodes);
        Description = _allCopies.Length == 1
            ? $"Duplicate Frame in '{FirstChainName()}'"
            : $"Duplicate {_allCopies.Length} Frames";
    }

    private string FirstChainName() => _ops[0] switch
    {
        DuplicateFrameBlockOp b => b.Chain.Name,
        DuplicateFrameAdjacentOp a => a.Chain.Name,
        _ => "Chain",
    };

    public bool Do()
    {
        if (_ops.Length == 0) return false;

        foreach (var block in _ops.OfType<DuplicateFrameBlockOp>()
                     .OrderByDescending(b => b.InsertIndex))
        {
            for (int i = 0; i < block.Copies.Length; i++)
                block.Chain.Frames.Insert(block.InsertIndex + i, block.Copies[i]);
        }

        foreach (var group in _ops.OfType<DuplicateFrameAdjacentOp>().GroupBy(a => a.Chain))
        {
            foreach (var entry in group.OrderByDescending(e => e.Chain.Frames.IndexOf(e.Source)))
            {
                int idx = entry.Chain.Frames.IndexOf(entry.Source) + 1;
                entry.Chain.Frames.Insert(Math.Min(idx, entry.Chain.Frames.Count), entry.Copy);
            }
        }

        RaiseSideEffects();
        _selectedState.SelectedNodes = _allCopies.Cast<object>().ToList();
        _selectedState.SelectedFrame = _allCopies[^1];
        return true;
    }

    public void Undo()
    {
        foreach (var op in _ops.Reverse())
        {
            switch (op)
            {
                case DuplicateFrameBlockOp b:
                    foreach (var copy in b.Copies.Reverse())
                        b.Chain.Frames.Remove(copy);
                    break;
                case DuplicateFrameAdjacentOp a:
                    a.Chain.Frames.Remove(a.Copy);
                    break;
            }
        }

        RaiseSideEffects();
        _selectedState.SelectedNodes = _preSelection;
        RestoreFrameSelection(_preSelection);
    }

    public void Redo() => Do();

    private void RaiseSideEffects()
    {
        foreach (var chain in _ops.Select(op => op switch
        {
            DuplicateFrameBlockOp b => b.Chain,
            DuplicateFrameAdjacentOp a => a.Chain,
            _ => null,
        }).Where(c => c is not null).Distinct())
            _commands.RefreshTreeNode(chain!);
        _events.RaiseAnimationChainsChanged();
        _commands.SaveCurrentAnimationChainList();
    }

    private void RestoreFrameSelection(List<object> nodes)
    {
        _selectedState.SelectedFrame = nodes.OfType<AnimationFrameSave>().LastOrDefault();
    }
}
