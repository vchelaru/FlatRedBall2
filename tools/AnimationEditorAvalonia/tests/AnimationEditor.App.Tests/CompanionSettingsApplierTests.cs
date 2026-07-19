using AnimationEditor.App.Controls;
using AnimationEditor.Core.Data;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

// #754 Phase B: the testable core of the browser build's companion-file (zoom/grid/guides)
// load and save wiring -- see CompanionSettingsApplier's doc comment for why the
// toolbar-checkbox glue around this stays untested in App.axaml.cs.
public class CompanionSettingsApplierTests
{
    [AvaloniaFact]
    public void Apply_GivenLoadedSettings_PushesZoomGridAndGuidesIntoControls()
    {
        var ctx = TestHelpers.BuildServices();
        var wireframe = ctx.CreateWireframeControl();
        var preview = ctx.CreatePreviewControl();
        var settings = new AESettingsSave
        {
            SnapToGrid = true,
            GridSize = 48,
            WireframeZoomPercent = 150,
            PreviewZoomPercent = 75,
            HorizontalGuides = new() { 10f },
            VerticalGuides = new() { 20f, 30f },
        };

        CompanionSettingsApplier.Apply(settings, wireframe, preview);

        Assert.Equal((true, 48), wireframe.GridState);
        Assert.Equal(1.5f, wireframe.CameraState.Zoom, 2);
        Assert.Equal(0.75f, preview.Zoom, 2);
        Assert.Equal(new[] { 10f }, preview.HGuides);
        Assert.Equal(new[] { 20f, 30f }, preview.VGuides);
    }

    [AvaloniaFact]
    public void Build_GivenControlState_ReturnsMatchingSettings()
    {
        var ctx = TestHelpers.BuildServices();
        var wireframe = ctx.CreateWireframeControl();
        var preview = ctx.CreatePreviewControl();
        wireframe.SetZoomPercent(200);
        preview.SetZoomPercent(50);
        preview.SetGuides(hGuides: new[] { 5f }, vGuides: new[] { 6f, 7f });

        var settings = CompanionSettingsApplier.Build(wireframe, preview, snapToGrid: true, gridSize: 32);

        Assert.True(settings.SnapToGrid);
        Assert.Equal(32, settings.GridSize);
        Assert.Equal(200, settings.WireframeZoomPercent);
        Assert.Equal(50, settings.PreviewZoomPercent);
        Assert.Equal(new[] { 5f }, settings.HorizontalGuides);
        Assert.Equal(new[] { 6f, 7f }, settings.VerticalGuides);
    }
}
