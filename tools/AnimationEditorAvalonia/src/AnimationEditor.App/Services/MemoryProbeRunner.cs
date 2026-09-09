using AnimationEditor.App.Controls;
using AnimationEditor.Core.Diagnostics;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AnimationEditor.App.Services;

/// <summary>
/// Drives repeated open → close cycles against a real (non-headless) window and records a
/// <see cref="MemorySnapshot"/> at each phase, for issue #949. Runs only when
/// <c>--memory-probe</c> is passed; a normal launch never constructs this.
/// </summary>
/// <remarks>
/// This is the deliberately untested wiring layer: it needs a live GPU-backed window, so the
/// testable parts (arg parsing, cross-cycle growth math) live in
/// <see cref="MemoryProbeOptions"/> and <see cref="MemoryProbeAnalysis"/>, which are covered
/// by Core tests.
/// </remarks>
internal sealed class MemoryProbeRunner
{
    // Long enough for the load to finish rendering and for Avalonia to settle its own deferred
    // work; short enough that a 5-cycle run stays under a minute.
    private static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(2);

    private readonly MainWindow _window;
    private readonly MemoryProbeOptions _options;
    private readonly List<MemorySnapshot> _snapshots = [];

    public MemoryProbeRunner(MainWindow window, MemoryProbeOptions options)
    {
        _window = window;
        _options = options;
    }

    public async Task RunAsync()
    {
        await SettleAsync();
        Record(MemoryProbePhase.Baseline, cycle: 0);

        for (int cycle = 1; cycle <= _options.Cycles; cycle++)
        {
            // Alternate when a second file was given: two distinct textures per pair of cycles,
            // instead of re-focusing one already-open tab.
            string? file = cycle % 2 == 0 && _options.SecondFilePath is { } second
                ? second
                : _options.FilePath;
            if (file != null)
                await _window.OpenFileAsTab(file);
            await SettleAsync();
            Record(MemoryProbePhase.Loaded, cycle);

            if (!_options.KeepOpen)
                await _window.CloseProjectAsync();
            await SettleAsync();
            Record(MemoryProbePhase.Closed, cycle);

            Collect();
            await SettleAsync();
            Record(MemoryProbePhase.Settled, cycle);
        }

        WriteReport();
    }

    /// <summary>
    /// Yields to the UI thread repeatedly rather than blocking it: the window has to keep
    /// rendering during the delay or Skia's caches never update and the reading is meaningless.
    /// </summary>
    private static async Task SettleAsync()
    {
        var deadline = DateTime.UtcNow + SettleDelay;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
    }

    // Two passes: the first can resurrect finalizable objects (SKImage and friends hold native
    // handles released from a finalizer), so a single collection under-reports what is actually
    // reclaimable and would fake a leak.
    private static void Collect()
    {
        for (int i = 0; i < 2; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private void Record(MemoryProbePhase phase, int cycle)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();

        _snapshots.Add(new MemorySnapshot(
            phase,
            cycle,
            process.PrivateMemorySize64,
            GC.GetTotalMemory(forceFullCollection: false),
            GC.GetGCMemoryInfo().TotalCommittedBytes,
            SKGraphics.GetResourceCacheTotalBytesUsed(),
            SKGraphics.GetFontCacheUsed(),
            _window.WireframeCtrl.GpuCache.UsedBytes));
    }

    private void WriteReport()
    {
        string path = _options.OutputPath
            ?? Path.Combine(Path.GetTempPath(), "ae-memory-probe.ndjson");

        var lines = new StringBuilder();
        foreach (var snapshot in _snapshots)
            lines.AppendLine(JsonSerializer.Serialize(snapshot));

        var analysis = MemoryProbeAnalysis.Analyze(_snapshots);
        lines.AppendLine(JsonSerializer.Serialize(new { analysis }));

        File.WriteAllText(path, lines.ToString());
    }
}
