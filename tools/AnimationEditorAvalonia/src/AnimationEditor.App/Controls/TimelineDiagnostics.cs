using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The timeline's share of the F3 diagnostics: how long the UI thread takes to record a frame, how long the render thread
/// takes to rasterize it (two marker draw operations bracket the frame, so the second timestamp minus the first is the
/// control's raster cost on whichever backend runs), the work counters in <see cref="TimelineFrameStats"/>, and a
/// once-a-second summary appended to the file named by the <c>AE_TIMELINE_PERF_LOG</c> environment variable (or
/// <see cref="LogPath"/>). Nothing here runs unless the overlay is on or a log path is set.
/// </summary>
internal sealed class TimelineDiagnostics
{
    private const int SampleWindow = 10;
    private static readonly TimeSpan RepaintInterval = TimeSpan.FromSeconds(1);

    private readonly Action _invalidate;
    private readonly RollingAverage _recordMs = new(SampleWindow);
    private readonly RollingAverage _rasterMs = new(SampleWindow);
    private DispatcherTimer? _timer;
    private bool _enabled;

    // Render-thread state.
    private long _beginStamp;
    private long _windowStart;
    private int _windowFrames;
    private double _windowRasterMs, _windowRasterMax, _windowRecordMs;
    private volatile string _backend = "?";
    private volatile string _lastFpsText = "";

    public TimelineDiagnostics(Action invalidate)
    {
        _invalidate = invalidate;
        LogPath = Environment.GetEnvironmentVariable("AE_TIMELINE_PERF_LOG");
    }

    /// <summary>The frame being recorded. Reset at the start of each render; the control's draw code increments it.</summary>
    public TimelineFrameStats Current { get; } = new();

    /// <summary>The finished stats of the previous frame.</summary>
    public TimelineFrameStats Last { get; private set; } = new();

    public string? LogPath { get; set; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
            {
                _timer ??= CreateTimer();
                _timer.Start();
            }
            else
            {
                _timer?.Stop();
            }
            _invalidate();
        }
    }

    public bool Active => _enabled || !string.IsNullOrEmpty(LogPath);

    /// <summary>The render-thread milliseconds of the newest finished frame; 0 until one has been measured.</summary>
    public double LastRasterMs { get; private set; }

    /// <summary>How many frames the render thread has finished since diagnostics started.</summary>
    public int RasterFrames { get; private set; }

    public void Stop() => _timer?.Stop();

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer { Interval = RepaintInterval };
        timer.Tick += (_, _) => _invalidate();
        return timer;
    }

    /// <summary>Records one frame with <paramref name="draw"/>, bracketed by the render-thread timing markers when diagnostics are active.</summary>
    public void Frame(DrawingContext context, Action<DrawingContext> draw, double overlayLeft, double overlayTop)
    {
        Current.Reset();
        bool active = Active;
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        if (active) context.Custom(new MarkerOp(this, begin: true, null));

        draw(context);

        Current.RecordMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        Current.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        Last = Current.Copy();
        if (active)
        {
            _recordMs.Add(Current.RecordMs);
            context.Custom(new MarkerOp(this, begin: false, Last));
        }
        if (_enabled) DrawOverlay(context, overlayLeft, overlayTop);
    }

    private void DrawOverlay(DrawingContext context, double left, double top)
    {
        string[] lines =
        {
            $"timeline: record {_recordMs.Average:F2} ms  raster {_rasterMs.Average:F2} ms  {_lastFpsText}  [{_backend}]",
            $"blocks {Last.BlocksIssued}/{Last.Blocks} (visible {Last.BlocksVisible})  cells {Last.CellsIssued} (visible {Last.CellsVisible})  images {Last.Images}  ops {Last.FilmstripOps}",
            $"clips {Last.Clips}  texts {Last.Texts}  hatch {Last.HatchLines}  grid {Last.GridLines}  ruler {Last.RulerLabels}  alloc {Last.AllocatedBytes / 1024} KB",
        };
        var typeface = new Typeface(FontFamily.Default);
        double width = 0, height = 0;
        var texts = new FormattedText[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            texts[i] = new FormattedText(lines[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11,
                new SolidColorBrush(Color.FromRgb(0, 255, 80)));
            width = Math.Max(width, texts[i].Width);
            height += texts[i].Height;
        }
        context.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), new Rect(left, top, width + 12, height + 8));
        double y = top + 4;
        foreach (var text in texts)
        {
            context.DrawText(text, new Point(left + 6, y));
            y += text.Height;
        }
    }

    // Runs on the render thread, in draw order: the begin marker, then everything the frame draws, then the end marker.
    private void OnMark(bool begin, bool gpu, TimelineFrameStats? stats)
    {
        long now = Stopwatch.GetTimestamp();
        _backend = gpu ? "GPU" : "CPU";
        if (begin)
        {
            _beginStamp = now;
            return;
        }
        double ms = (now - _beginStamp) * 1000.0 / Stopwatch.Frequency;
        _rasterMs.Add(ms);
        LastRasterMs = ms;
        RasterFrames++;
        _windowFrames++;
        _windowRasterMs += ms;
        _windowRasterMax = Math.Max(_windowRasterMax, ms);
        _windowRecordMs += stats?.RecordMs ?? 0;
        if (_windowStart == 0) _windowStart = now;
        double elapsed = (now - _windowStart) / (double)Stopwatch.Frequency;
        if (elapsed < 1.0) return;

        double fps = _windowFrames / elapsed;
        _lastFpsText = $"{fps:F0} fps";
        if (!string.IsNullOrEmpty(LogPath) && stats is not null)
        {
            string line = string.Format(CultureInfo.InvariantCulture,
                "{0:HH:mm:ss.fff} [{1}] fps {2:F1} frames {3} raster avg {4:F2} max {5:F2} ms record avg {6:F2} ms | {7}{8}",
                DateTime.Now, _backend, fps, _windowFrames, _windowRasterMs / _windowFrames, _windowRasterMax,
                _windowRecordMs / _windowFrames, stats, Environment.NewLine);
            try { File.AppendAllText(LogPath, line); } catch (IOException) { }
        }
        _windowStart = now;
        _windowFrames = 0;
        _windowRasterMs = _windowRasterMax = _windowRecordMs = 0;
    }

    private sealed class MarkerOp(TimelineDiagnostics owner, bool begin, TimelineFrameStats? stats) : ICustomDrawOperation
    {
        public Rect Bounds => new(0, 0, 1, 1);
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (lease is null)
            {
                owner.OnMark(begin, false, stats);
                return;
            }
            using (lease)
                owner.OnMark(begin, lease.GrContext != null, stats);
        }
    }
}
