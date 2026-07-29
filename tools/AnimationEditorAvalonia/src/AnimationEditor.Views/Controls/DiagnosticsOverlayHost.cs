using AnimationEditor.Core.Rendering;
using Avalonia.Threading;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Shared host for the render-diagnostics overlay (#514): owns the on/off flag, the rolling
/// draw-time sampler, and the idle-repaint timer that keeps the readout live. Used by every
/// canvas panel that exposes a <c>DiagnosticsEnabled</c> toggle (<see cref="TextureViewport"/>,
/// <see cref="PreviewControl"/>) so the timer interval and the "is it on?" bookkeeping exist once.
/// <para>
/// Lives in <c>AnimationEditor.Views</c> rather than Core because it drives an Avalonia
/// <see cref="DispatcherTimer"/>; the averaging math it feeds is the pure
/// <see cref="RollingAverage"/> in Core.
/// </para>
/// </summary>
internal sealed class DiagnosticsOverlayHost
{
    // Draw-time samples averaged for the readout. One host = one panel's sampler.
    private const int SampleWindow = 10;

    // The panels only repaint on demand (pan/zoom/playback/selection), so an idle overlay would
    // show a frozen ms/frame. While diagnostics are on, tick a 1 fps repaint so the readout stays
    // live even when nothing else changes; stop it otherwise to keep the panel idle.
    private static readonly TimeSpan RepaintInterval = TimeSpan.FromSeconds(1);

    private readonly Action _invalidate;
    private readonly RollingAverage _drawTimes = new(SampleWindow);
    private DispatcherTimer? _repaintTimer;
    private bool _enabled;

    public DiagnosticsOverlayHost(Action invalidate) => _invalidate = invalidate;

    /// <summary>
    /// Whether the overlay is showing. Toggling starts/stops the idle-repaint timer and requests
    /// a repaint; setting the current value is a no-op.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
            {
                _repaintTimer ??= CreateRepaintTimer();
                _repaintTimer.Start();
            }
            else
                _repaintTimer?.Stop();
            _invalidate();
        }
    }

    /// <summary>
    /// The sampler to hand a draw op, or <c>null</c> while diagnostics are off — which is also the
    /// draw op's signal to skip timing entirely, so the cost only lands where you're profiling.
    /// </summary>
    public RollingAverage? ActiveSampler => _enabled ? _drawTimes : null;

    /// <summary>Stops the idle-repaint timer. Call from the host's detach handler.</summary>
    public void Stop() => _repaintTimer?.Stop();

    private DispatcherTimer CreateRepaintTimer()
    {
        var timer = new DispatcherTimer { Interval = RepaintInterval };
        timer.Tick += (_, _) => _invalidate();
        return timer;
    }
}
