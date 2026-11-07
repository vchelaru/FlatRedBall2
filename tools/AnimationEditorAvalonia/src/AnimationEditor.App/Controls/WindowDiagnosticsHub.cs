using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The window-wide half of the F3 diagnostics, which the per-panel overlays cannot see: how long the render thread takes
/// per whole frame (two <see cref="RenderFrameProbe"/> markers, drawn first and last in the window, bracket everything
/// that frame draws), how long the UI thread is unresponsive (a watcher thread posts a no-op to it every few milliseconds
/// and times how late it runs), and what the garbage collector is doing. Off until <see cref="Enabled"/>; stalls longer
/// than <see cref="StallLogMilliseconds"/> go to <see cref="LogPath"/> with the collector's share of the same moment.
/// </summary>
internal sealed class WindowDiagnosticsHub
{
    public const double StallLogMilliseconds = 16;
    private static readonly TimeSpan WatchPeriod = TimeSpan.FromMilliseconds(4);

    private readonly BurstRate _frames = new();
    private readonly RollingAverage _frameMs = new(10);
    private long _beginStamp;
    private volatile string _backend = "?";

    private Timer? _watcher;
    private int _pending;
    private readonly object _stallLock = new();
    private readonly List<(long Stamp, double Ms)> _stalls = new();
    private double _worstSinceLog;

    private long _gcStamp;
    private int _gc0, _gc1, _gc2;
    private TimeSpan _gcPause;
    private string _gcLine = "gc: (sampling)";

    private bool _enabled;

    public string? LogPath { get; set; } = Environment.GetEnvironmentVariable("AE_TIMELINE_PERF_LOG");

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
            {
                _watcher = new Timer(_ => Probe(), null, WatchPeriod, WatchPeriod);
                SampleGc(force: true);
            }
            else
            {
                _watcher?.Dispose();
                _watcher = null;
            }
        }
    }

    // Runs on the render thread, in draw order: the begin marker first, the end marker last.
    internal void OnFrameMark(bool begin, bool gpu)
    {
        long now = Stopwatch.GetTimestamp();
        _backend = gpu ? "GPU" : "CPU";
        if (begin)
        {
            _beginStamp = now;
            _frames.Add(now / (double)Stopwatch.Frequency);
        }
        else if (_beginStamp != 0)
        {
            _frameMs.Add((now - _beginStamp) * 1000.0 / Stopwatch.Frequency);
        }
    }

    // Runs on the watcher thread: ask the UI thread to answer; how late it does is how long it was busy.
    private void Probe()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1) return;
        long posted = Stopwatch.GetTimestamp();
        Dispatcher.UIThread.Post(() =>
        {
            double ms = (Stopwatch.GetTimestamp() - posted) * 1000.0 / Stopwatch.Frequency;
            Volatile.Write(ref _pending, 0);
            if (ms < WatchPeriod.TotalMilliseconds * 2) return;
            RecordStall(ms);
        }, DispatcherPriority.Normal);
    }

    private void RecordStall(double ms)
    {
        long now = Stopwatch.GetTimestamp();
        lock (_stallLock)
        {
            _stalls.Add((now, ms));
            _stalls.RemoveAll(s => (now - s.Stamp) > Stopwatch.Frequency);
            _worstSinceLog = Math.Max(_worstSinceLog, ms);
        }
        if (ms < StallLogMilliseconds || string.IsNullOrEmpty(LogPath)) return;
        string line = string.Format(CultureInfo.InvariantCulture,
            "{0:HH:mm:ss.fff} UI thread stall {1:F1} ms | gc0 {2} gc1 {3} gc2 {4} pause {5:F1} ms in total{6}",
            DateTime.Now, ms, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            GC.GetTotalPauseDuration().TotalMilliseconds, Environment.NewLine);
        try { File.AppendAllText(LogPath, line); } catch (IOException) { }
    }

    /// <summary>The overlay lines for this moment. Cheap; called when the timeline draws its own overlay.</summary>
    public IReadOnlyList<string> Lines()
    {
        SampleGc(force: false);
        double worst = 0;
        int over = 0;
        long now = Stopwatch.GetTimestamp();
        lock (_stallLock)
        {
            foreach (var (stamp, ms) in _stalls)
            {
                if (now - stamp > Stopwatch.Frequency) continue;
                worst = Math.Max(worst, ms);
                if (ms >= StallLogMilliseconds) over++;
            }
        }
        return new[]
        {
            $"window: render frames {_frames.Hertz:F0}/s (worst gap {_frames.WorstGapMilliseconds:F0} ms)  whole-frame raster {_frameMs.Average:F2} ms  [{_backend}]",
            $"ui thread (last second): worst stall {worst:F0} ms, {over} over {StallLogMilliseconds:F0} ms",
            _gcLine,
        };
    }

    private void SampleGc(bool force)
    {
        long now = Stopwatch.GetTimestamp();
        double seconds = (now - _gcStamp) / (double)Stopwatch.Frequency;
        if (!force && seconds < 0.5) return;
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        var pause = GC.GetTotalPauseDuration();
        if (_gcStamp != 0 && seconds > 0)
        {
            _gcLine = string.Format(CultureInfo.InvariantCulture,
                "gc: gen0 {0:F0}/s gen1 {1:F0}/s gen2 {2:F0}/s  paused {3:F1} ms/s  heap {4} MB",
                (g0 - _gc0) / seconds, (g1 - _gc1) / seconds, (g2 - _gc2) / seconds,
                (pause - _gcPause).TotalMilliseconds / seconds, GC.GetTotalMemory(false) / (1024 * 1024));
        }
        _gcStamp = now;
        _gc0 = g0;
        _gc1 = g1;
        _gc2 = g2;
        _gcPause = pause;
    }
}

/// <summary>
/// A transparent, input-free layer that stretches over the window and tells the <see cref="WindowDiagnosticsHub"/> when
/// the render thread reaches it. One placed first in the window and one last bracket a whole frame's drawing.
/// </summary>
public sealed class RenderFrameProbe : Control
{
    internal WindowDiagnosticsHub? Hub { get; set; }

    /// <summary>True for the probe that ends the frame (drawn last); false for the one that starts it.</summary>
    public bool IsEnd { get; set; }

    public RenderFrameProbe() => IsHitTestVisible = false;

    public override void Render(DrawingContext context)
    {
        if (Hub is { } hub && hub.Enabled)
            context.Custom(new MarkerOp(hub, !IsEnd, new Rect(Bounds.Size)));
    }

    private sealed class MarkerOp(WindowDiagnosticsHub hub, bool begin, Rect bounds) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (lease is null)
            {
                hub.OnFrameMark(begin, false);
                return;
            }
            using (lease)
                hub.OnFrameMark(begin, lease.GrContext != null);
        }
    }
}
