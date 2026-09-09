using AnimationEditor.Core.Diagnostics;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #949: repeated open -> close cycles grow private bytes with no plateau. The analysis
// answers "how many bytes does one full cycle retain, and in which counter", which is what
// separates a managed leak from a native (Skia/GPU cache) one.
public class MemoryProbeAnalysisTests
{
    // Only Settled snapshots (post-close, post-GC) are comparable across cycles -- Loaded
    // snapshots include the project that is about to be released.
    private static MemorySnapshot Settled(
        int cycle, long privateBytes, long managedBytes, long gpuCacheBytes) =>
        new(MemoryProbePhase.Settled, cycle, privateBytes, managedBytes,
            GcCommittedBytes: 0, SkiaResourceCacheBytes: 0,
            SkiaFontCacheBytes: 0, GpuResourceCacheBytes: gpuCacheBytes);

    [Fact]
    public void Analyze_NativeGrowsWhileManagedFlat_ReportsGrowthInNativeCounterOnly()
    {
        // Managed heap identical every cycle; private bytes climb 8MB per cycle -- the
        // signature of a native leak, which is what #949 suspects.
        MemorySnapshot[] snapshots =
        [
            Settled(cycle: 1, privateBytes: 100_000_000, managedBytes: 9_000_000, gpuCacheBytes: 4_000_000),
            Settled(cycle: 2, privateBytes: 108_000_000, managedBytes: 9_000_000, gpuCacheBytes: 12_000_000),
            Settled(cycle: 3, privateBytes: 116_000_000, managedBytes: 9_000_000, gpuCacheBytes: 20_000_000),
        ];

        var analysis = MemoryProbeAnalysis.Analyze(snapshots);

        Assert.Equal(8_000_000, analysis.PrivateBytesPerCycle);
        Assert.Equal(0, analysis.ManagedHeapBytesPerCycle);
        Assert.Equal(8_000_000, analysis.GpuResourceCacheBytesPerCycle);
    }

    [Fact]
    public void Analyze_NonSettledPhasesPresent_IgnoresThemWhenComputingPerCycleGrowth()
    {
        // A Loaded snapshot in the middle must not be mistaken for a cycle boundary.
        MemorySnapshot[] snapshots =
        [
            Settled(cycle: 1, privateBytes: 100_000_000, managedBytes: 9_000_000, gpuCacheBytes: 0),
            new(MemoryProbePhase.Loaded, 2, PrivateBytes: 900_000_000, ManagedHeapBytes: 50_000_000,
                GcCommittedBytes: 0, SkiaResourceCacheBytes: 0, SkiaFontCacheBytes: 0,
                GpuResourceCacheBytes: 0),
            Settled(cycle: 2, privateBytes: 104_000_000, managedBytes: 9_000_000, gpuCacheBytes: 0),
        ];

        var analysis = MemoryProbeAnalysis.Analyze(snapshots);

        Assert.Equal(4_000_000, analysis.PrivateBytesPerCycle);
    }

    [Fact]
    public void Analyze_SingleSettledSnapshot_ReportsZeroPerCycleGrowth()
    {
        // One cycle gives no slope to measure; reporting a growth number would be a lie.
        MemorySnapshot[] snapshots =
        [
            Settled(cycle: 1, privateBytes: 100_000_000, managedBytes: 9_000_000, gpuCacheBytes: 0),
        ];

        var analysis = MemoryProbeAnalysis.Analyze(snapshots);

        Assert.Equal(0, analysis.PrivateBytesPerCycle);
    }
}
