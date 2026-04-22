using System.Collections.Generic;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Diagnostics;

public class RenderDiagnostics
{
    private readonly List<BatchBreakInfo> _breaks = new();

    public bool IsEnabled { get; set; }
    public int BatchBreakCount => _breaks.Count;
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
