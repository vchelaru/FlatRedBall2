using System.Collections.Generic;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Per-frame rendering instrumentation. When <see cref="IsEnabled"/> is <c>true</c>, the
/// rendering pipeline records every <see cref="IRenderBatch"/> transition into
/// <see cref="BatchBreaks"/>; when disabled, recording is a no-op and adds no overhead.
/// Reset every frame — inspect after the draw pass and before the next frame begins.
/// Access via <see cref="FlatRedBallService.RenderDiagnostics"/>.
/// </summary>
public class RenderDiagnostics
{
    private readonly List<BatchBreakInfo> _breaks = new();

    /// <summary>
    /// When <c>true</c>, the renderer records every batch transition into <see cref="BatchBreaks"/>.
    /// Off by default — turn on while diagnosing draw-call counts; leave off in shipping builds.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>Number of batch transitions recorded for the current frame. Equivalent to <c>BatchBreaks.Count</c>.</summary>
    public int BatchBreakCount => _breaks.Count;

    /// <summary>
    /// Detailed record of each batch transition this frame. Cleared at the start of every frame —
    /// inspect after the draw pass completes and before the next frame begins.
    /// </summary>
    public IReadOnlyList<BatchBreakInfo> BatchBreaks => _breaks;

    internal void BeginFrame() => _breaks.Clear();

    internal void RecordBreak(IRenderBatch previous, IRenderBatch next, Layer? layer, float z,
        string previousName, string nextName)
    {
        _breaks.Add(new BatchBreakInfo
        {
            PreviousBatch = previous,
            NextBatch = next,
            Layer = layer,
            Z = z,
            PreviousObjectName = previousName,
            NextObjectName = nextName
        });
    }
}
