namespace AnimationEditor.App.Controls;

/// <summary>
/// What one <see cref="SequenceTimelineControl"/> render issued. "Issued" counts draw work handed to Avalonia;
/// "Visible" counts the part of it that lands inside the lanes, so the difference is work spent on things that are off
/// screen. The F3 overlay, the perf log and the culling regression tests all read this.
/// </summary>
internal sealed class TimelineFrameStats
{
    public int Blocks, BlocksIssued, BlocksVisible;
    public int Cells, CellsIssued, CellsVisible;
    public int Images, Clips, Texts, HatchLines, GridLines, RulerLabels;
    public int Markers, MarkersIssued;
    public int TextsCreated;
    public int FilmstripOps;

    /// <summary>Milliseconds the UI thread spent recording the frame (<c>Render</c>), not the raster that follows it.</summary>
    public double RecordMs;

    /// <summary>Bytes the UI thread allocated while recording the frame.</summary>
    public long AllocatedBytes;

    public void Reset()
    {
        Blocks = BlocksIssued = BlocksVisible = 0;
        Cells = CellsIssued = CellsVisible = 0;
        Images = Clips = Texts = HatchLines = GridLines = RulerLabels = 0;
        Markers = MarkersIssued = 0;
        TextsCreated = 0;
        FilmstripOps = 0;
        RecordMs = 0;
        AllocatedBytes = 0;
    }

    public TimelineFrameStats Copy() => (TimelineFrameStats)MemberwiseClone();

    public override string ToString() =>
        $"blocks {BlocksIssued}/{Blocks} (vis {BlocksVisible}) cells {CellsIssued} (vis {CellsVisible}) images {Images} clips {Clips} "
        + $"texts {Texts} hatch {HatchLines} grid {GridLines} ruler {RulerLabels} markers {MarkersIssued}/{Markers} "
        + $"record {RecordMs:F2}ms alloc {AllocatedBytes / 1024}KB";
}
