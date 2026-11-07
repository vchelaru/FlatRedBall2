using System;
using System.Collections.Generic;
using Avalonia;

namespace AnimationEditor.App.Controls;

// F4, while the F3 overlay is on, cycles what the timeline skips drawing, so the cost of each part of a frame can be read off
// the overlay. Diagnostics only: it changes what is drawn.
public sealed partial class SequenceTimelineControl
{
    private TimelineProbeMode _probeMode;

    /// <summary>What the timeline currently skips drawing; <see cref="TimelineProbeMode.Normal"/> draws everything.</summary>
    public TimelineProbeMode ProbeMode
    {
        get => _probeMode;
        set
        {
            if (_probeMode == value) return;
            _probeMode = value;
            _filmstrip.PerCellDraws = value == TimelineProbeMode.PerCellDraws;
            _filmstrip.WaitForGpu = value == TimelineProbeMode.WaitForGpuAfterFilmstrips;
            ClearThemeCaches();   // the text cache holds blank texts while the text is probed away
            InvalidateVisual();
        }
    }

    private void CycleProbeMode() =>
        ProbeMode = (TimelineProbeMode)(((int)ProbeMode + 1) % Enum.GetValues<TimelineProbeMode>().Length);

    private bool ProbesTextAway => ProbeMode is TimelineProbeMode.NoFilmstripsOrText or TimelineProbeMode.BackgroundOnly;

    private IReadOnlyList<string> ProbeOverlayLines()
    {
        var probe = _filmstrip.Probe;
        var (milliseconds, changes) = Diagnostics.LastFilmstripFrame;
        return new[]
        {
            $"probe (F4 cycles): {ProbeMode}   filmstrip draws {milliseconds:F2} ms on the render thread",
            $"gpu cache: {probe.Bytes / (1024.0 * 1024):F1} MB of {probe.Limit / (1024 * 1024)} MB, {probe.Resources} resources, changed {changes}x last frame   atlas pages {FilmstripAtlasPages}",
        };
    }
}
