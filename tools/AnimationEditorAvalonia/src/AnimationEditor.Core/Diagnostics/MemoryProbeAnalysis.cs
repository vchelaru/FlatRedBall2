namespace AnimationEditor.Core.Diagnostics;

/// <summary>
/// Bytes retained by one full open/close cycle, broken out per counter so a managed leak
/// (managed heap grows) is distinguishable from a native one (private bytes grow while the
/// managed heap stays flat).
/// </summary>
public sealed record MemoryProbeAnalysis(
    int SettledCycleCount,
    long PrivateBytesPerCycle,
    long ManagedHeapBytesPerCycle,
    long SkiaResourceCacheBytesPerCycle,
    long SkiaFontCacheBytesPerCycle,
    long GpuResourceCacheBytesPerCycle)
{
    /// <summary>
    /// Averages growth across the <see cref="MemoryProbePhase.Settled"/> readings. Other phases
    /// are ignored: a Loaded reading still holds the project that the cycle is about to release,
    /// so including it would report a spike as retention. Fewer than two settled readings gives
    /// no slope, so every per-cycle figure comes back zero rather than guessing from one point.
    /// </summary>
    public static MemoryProbeAnalysis Analyze(IReadOnlyList<MemorySnapshot> snapshots)
    {
        var settled = snapshots
            .Where(s => s.Phase == MemoryProbePhase.Settled)
            .OrderBy(s => s.Cycle)
            .ToList();

        if (settled.Count < 2)
            return new MemoryProbeAnalysis(settled.Count, 0, 0, 0, 0, 0);

        MemorySnapshot first = settled[0];
        MemorySnapshot last = settled[^1];
        int spans = settled.Count - 1;

        long PerCycle(Func<MemorySnapshot, long> counter) =>
            (counter(last) - counter(first)) / spans;

        return new MemoryProbeAnalysis(
            settled.Count,
            PerCycle(s => s.PrivateBytes),
            PerCycle(s => s.ManagedHeapBytes),
            PerCycle(s => s.SkiaResourceCacheBytes),
            PerCycle(s => s.SkiaFontCacheBytes),
            PerCycle(s => s.GpuResourceCacheBytes));
    }
}
