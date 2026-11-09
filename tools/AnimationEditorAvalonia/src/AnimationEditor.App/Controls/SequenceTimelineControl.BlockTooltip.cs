using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.App.Controls;

public sealed partial class SequenceTimelineControl
{
    private DispatcherTimer? _tipTimer;
    private SequenceChainReferenceSave? _tipReference;
    private Point _tipPoint;

    /// <summary>
    /// The full text of the block under the pointer (<c>Walk ×2 (1.2 s)</c>), once the pointer has rested on it for
    /// <see cref="TimelineTooltips.ShowDelay"/>, or null. Drawn, not a ToolTip, like the eye tooltip, so a test can read it.
    /// </summary>
    public string? BlockTooltip { get; private set; }


    // The tooltip belongs to one block: moving onto another restarts the delay, and any drag, scroll or press ends it.
    private void UpdateBlockTooltip(Point point)
    {
        TimelineBlock? block = null;
        if (_drag == DragKind.None && !_scrubbing && !_panning && _chipGhost is null && point.X >= Viewport.ColumnWidth && Sequence is { } sequence)
            block = TimelineLayout.HitTest(Viewport, _blocks, sequence.Tracks.Count, point.X, point.Y)?.Block;
        if (block is null)
        {
            ClearBlockTooltip();
            return;
        }
        if (ReferenceEquals(block.Reference, _tipReference))
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
        _tipReference = block.Reference;
        _tipPoint = point;
        string text = block.IsMissing
            ? $"⚠ {block.Reference.Chain} (missing)"
            : BlockLabel.Tooltip(block.Reference.Chain, block.Reference.Loops, block.Played);
        _tipTimer = new DispatcherTimer { Interval = TimelineTooltips.ShowDelay };
        _tipTimer.Tick += (_, _) =>
        {
            _tipTimer?.Stop();
            BlockTooltip = text;
            InvalidatePopups();
        };
        _tipTimer.Start();
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
