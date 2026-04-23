using FlatRedBall2.Rendering;

namespace FlatRedBall2.Diagnostics;

/// <summary>
/// Diagnostic record describing a single batch transition during rendering. Each break is
/// a <c>SpriteBatch.End</c>/<c>Begin</c> pair caused by two adjacent renderables disagreeing
/// on batch (e.g. shapes followed by sprites). Excessive breaks tank fill-rate; consult
/// <see cref="RenderDiagnostics.BatchBreaks"/> when investigating draw-call counts.
/// </summary>
public readonly struct BatchBreakInfo
{
    /// <summary>The batch active immediately before the break.</summary>
    public IRenderBatch PreviousBatch { get; init; }

    /// <summary>The batch that caused the break by being different from <see cref="PreviousBatch"/>.</summary>
    public IRenderBatch NextBatch { get; init; }

    /// <summary>The layer the renderables on both sides of the break belong to.</summary>
    public Layer? Layer { get; init; }

    /// <summary>The Z value of the renderable that triggered the transition (the "next" object).</summary>
    public float Z { get; init; }

    /// <summary>Name of the last renderable drawn in <see cref="PreviousBatch"/>, or empty if unnamed.</summary>
    public string PreviousObjectName { get; init; }

    /// <summary>Name of the first renderable that flipped to <see cref="NextBatch"/>, or empty if unnamed.</summary>
    public string NextObjectName { get; init; }
}
