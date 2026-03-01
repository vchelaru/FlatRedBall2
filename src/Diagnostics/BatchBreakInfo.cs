using FlatRedBall2.Rendering;

namespace FlatRedBall2.Diagnostics;

public readonly struct BatchBreakInfo
{
    public IRenderBatch PreviousBatch { get; init; }
    public IRenderBatch NextBatch { get; init; }
    public Layer Layer { get; init; }
    public float Z { get; init; }
    public string PreviousObjectName { get; init; }
    public string NextObjectName { get; init; }
}
