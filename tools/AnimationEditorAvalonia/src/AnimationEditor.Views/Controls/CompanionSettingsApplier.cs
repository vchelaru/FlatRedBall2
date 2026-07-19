using AnimationEditor.Core.Data;
using System;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Maps an <see cref="AESettingsSave"/> companion file to/from the shared
/// <see cref="WireframeControl"/>/<see cref="PreviewControl"/> state -- zoom, grid-snap, and guide
/// positions. The testable core of the companion-file load/save path; toolbar-checkbox syncing
/// and *when* to call <see cref="Build"/>/<see cref="Apply"/> stay in each host (desktop's
/// MainWindow, browser's App.axaml.cs), since those are Avalonia-host-specific and untested by
/// established precedent.
/// </summary>
public static class CompanionSettingsApplier
{
    /// <summary>Pushes a loaded companion file's zoom/grid/guides into the live controls.</summary>
    public static void Apply(AESettingsSave settings, WireframeControl wireframe, PreviewControl preview)
    {
        wireframe.SetGrid(settings.SnapToGrid, settings.GridSize);
        wireframe.SetZoomPercent(settings.WireframeZoomPercent);
        preview.SetZoomPercent(settings.PreviewZoomPercent);
        preview.SetGuides(settings.HorizontalGuides, settings.VerticalGuides);
    }

    /// <summary>Reads the controls' current zoom/guides into a new companion settings object.
    /// Grid-snap and grid size come from the caller since they live on a toolbar toggle/textbox,
    /// not on <paramref name="wireframe"/> itself.</summary>
    public static AESettingsSave Build(WireframeControl wireframe, PreviewControl preview, bool snapToGrid, int gridSize) =>
        new()
        {
            SnapToGrid = snapToGrid,
            GridSize = gridSize,
            WireframeZoomPercent = (int)MathF.Round(wireframe.Zoom * 100f),
            PreviewZoomPercent = (int)MathF.Round(preview.Zoom * 100f),
            HorizontalGuides = preview.HGuides.ToList(),
            VerticalGuides = preview.VGuides.ToList(),
        };
}
