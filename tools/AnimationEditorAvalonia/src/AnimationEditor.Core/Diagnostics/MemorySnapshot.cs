namespace AnimationEditor.Core.Diagnostics;

/// <summary>
/// Where in an open/close cycle a <see cref="MemorySnapshot"/> was taken. Only
/// <see cref="Settled"/> readings are comparable across cycles.
/// </summary>
public enum MemoryProbePhase
{
    /// <summary>Window is up and rendering, no project has been loaded yet.</summary>
    Baseline,

    /// <summary>A project is loaded and has rendered at least one frame.</summary>
    Loaded,

    /// <summary>The project has been closed but no collection has been forced yet.</summary>
    Closed,

    /// <summary>Post-close, post-collection. Anything still held here is retained across the cycle.</summary>
    Settled,
}

/// <summary>
/// One memory reading. All byte counts are absolute process-wide totals, not deltas —
/// <see cref="MemoryProbeAnalysis"/> derives the deltas.
/// </summary>
/// <param name="Cycle">1-based open/close iteration; 0 for the pre-cycle baseline.</param>
/// <param name="PrivateBytes">Committed private bytes — the number Task Manager and VS report growing.</param>
/// <param name="GpuResourceCacheBytes">Skia's <c>GRContext</c> GPU cache, sampled on the render thread; 0 on the software path.</param>
public sealed record MemorySnapshot(
    MemoryProbePhase Phase,
    int Cycle,
    long PrivateBytes,
    long ManagedHeapBytes,
    long GcCommittedBytes,
    long SkiaResourceCacheBytes,
    long SkiaFontCacheBytes,
    long GpuResourceCacheBytes);
