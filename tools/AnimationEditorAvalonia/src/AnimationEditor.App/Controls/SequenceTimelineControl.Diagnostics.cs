using Avalonia;
using Avalonia.VisualTree;

namespace AnimationEditor.App.Controls;

// The F3 overlay's timeline section and the counters behind it; see TimelineDiagnostics.
public sealed partial class SequenceTimelineControl
{
    private TimelineDiagnostics? _diagnostics;

    private TimelineDiagnostics Diagnostics => _diagnostics ??= new TimelineDiagnostics(InvalidateVisual);

    /// <summary>Shows the draw-time overlay (record and raster milliseconds, fps, GPU or CPU, what was drawn) over the lanes. Toggled with F3.</summary>
    public bool DiagnosticsEnabled
    {
        get => Diagnostics.Enabled;
        set => Diagnostics.Enabled = value;
    }

    /// <summary>Appends a once-a-second summary of the frames drawn to this file; null turns it off. Also read from <c>AE_TIMELINE_PERF_LOG</c> at startup.</summary>
    public string? PerfLogPath
    {
        get => Diagnostics.LogPath;
        set => Diagnostics.LogPath = value;
    }

    /// <summary>What the last render issued and how long recording it took.</summary>
    internal TimelineFrameStats LastFrame => Diagnostics.Last;

    internal TimelineDiagnostics DiagnosticsHost => Diagnostics;

    private TimelineFrameStats Stats => Diagnostics.Current;

    internal int FilmstripAtlasGeneration => _filmstrip.Atlas.Generation;

    internal int FilmstripAtlasPages => _filmstrip.Atlas.PageCount;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _diagnostics?.Stop();
    }
}
