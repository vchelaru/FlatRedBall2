using System;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.App.Controls;

public sealed partial class SequenceTimelineControl
{
    private DispatcherTimer? _tipTimer;
    // The chain placement or reference clip the tooltip belongs to.
    private object? _tipReference;
    private Point _tipPoint;

    /// <summary>
    /// The full text of the block under the pointer (<c>Walk ×2 (1.2 s)</c>), or why a reference clip plays nothing, once the
    /// pointer has rested on it for <see cref="TimelineTooltips.ShowDelay"/>, or null. Drawn, not a ToolTip, like the eye tooltip, so a test can read it.
    /// </summary>
    public string? BlockTooltip { get; private set; }


    // The tooltip belongs to one block: moving onto another restarts the delay, and any drag, scroll or press ends it.
    private void UpdateBlockTooltip(Point point)
    {
        object? key = null;
        string? text = null;
        if (_drag == DragKind.None && !_scrubbing && !_panning && _chipGhost is null && point.X >= Viewport.ColumnWidth && Sequence is { } sequence)
        {
            if (TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, point.X, point.Y)?.Block is { } block)
            {
                key = block.Reference;
                text = block.IsMissing
                    ? $"⚠ {block.Reference.Chain} (missing)"
                    : BlockLabel.Tooltip(block.Reference.Chain, block.Reference.Loops, block.Played);
            }
            // A clip that plays nothing has no filmstrip to hover, so its tooltip says why, as a missing chain's does.
            else if (TimelineReferences.HitTest(Viewport, _references, sequence.Tracks.Count, point.X, point.Y)?.Reference is { IsBroken: true } clip)
            {
                key = clip.Reference;
                text = clip.Cycle is { } cycle ? $"⚠ {SequenceCycleNotice.Describe(cycle)}" : $"⚠ {clip.Reference.Sequence} (missing)";
            }
        }
        if (key is null || text is null)
        {
            ClearBlockTooltip();
            return;
        }
        if (ReferenceEquals(key, _tipReference))
        {
            // The pill follows the pointer, before and after it shows.
            if (_tipPoint != point)
            {
                _tipPoint = point;
                if (BlockTooltip is not null) InvalidatePopups();
            }
            return;
        }
        ClearBlockTooltip();
        _tipReference = key;
        _tipPoint = point;
        _tipTimer = new DispatcherTimer { Interval = TimelineTooltips.ShowDelay };
        _tipTimer.Tick += (_, _) =>
        {
            _tipTimer?.Stop();
            BlockTooltip = text;
            InvalidatePopups();
        };
        _tipTimer.Start();
    }

    /// <summary>Raised with the (source, chain) of the chain block under the pointer, or nulls when there is none. UI-only.</summary>
    public event Action<string?, string?>? HoveredChainChanged;

    private (string, string)? _hoveredChain;

    private void UpdateHoveredChain(Point? point)
    {
        (string, string)? hovered = null;
        if (point is { } p && _drag == DragKind.None && !_scrubbing && !_panning && _chipGhost is null && p.X >= Viewport.ColumnWidth && Sequence is { } sequence
            && TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, p.X, p.Y)?.Block is { } block)
            hovered = (block.Reference.Source, block.Reference.Chain);
        if (hovered == _hoveredChain) return;
        _hoveredChain = hovered;
        HoveredChainChanged?.Invoke(hovered?.Item1, hovered?.Item2);
    }

    private void ClearBlockTooltip()
    {
        _tipTimer?.Stop();
        _tipTimer = null;
        _tipReference = null;
        if (BlockTooltip is null) return;
        BlockTooltip = null;
        InvalidatePopups();
    }
}
