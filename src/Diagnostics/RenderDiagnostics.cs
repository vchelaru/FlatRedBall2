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

    private int _internalDrawCallCount;

    /// <summary>
    /// Latest <see cref="Rendering.IRenderBatch.InternalDrawCallCount"/> reported this frame —
    /// GPU draw calls issued by a batch that wraps a foreign renderer (Gum, Apos.Shapes) FRB's own
    /// <see cref="BatchBreakCount"/> tracking above can't see into. Zero unless a batch in the
    /// scene overrides the default. Reset every frame, same as <see cref="BatchBreaks"/>.
    /// </summary>
    public int InternalDrawCallCount => _internalDrawCallCount;

    internal void BeginFrame()
    {
        _breaks.Clear();
        _internalDrawCallCount = 0;
    }

    /// <summary>
    /// Records this frame's <see cref="InternalDrawCallCount"/>. Called by the engine after every
    /// <c>IRenderBatch.End</c>. Overwrites rather than accumulates: a batch's own reported count is
    /// already a running total for the whole host frame (e.g. GumRenderBatch reads Gum's internal
    /// counter, which Gum resets once per frame, not once per Begin/End cycle — and Gum's Begin/End
    /// runs multiple times per frame, once per camera plus the overlay pass), so summing successive
    /// reports would double-count every earlier cycle.
    /// </summary>
    internal void RecordInternalDrawCalls(int count) => _internalDrawCallCount = count;

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
