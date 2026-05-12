using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests that <see cref="PreviewControl"/> displays the world coordinate value
/// as a text label next to a guide while it is being dragged.
/// </summary>
public class GuideValueLabelTests
{
    private static void ResetSingletons()
    {
        ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
        ProjectManager.Self.FileName = null;
        SelectedState.Self.SelectedChain = null;
        SelectedState.Self.SelectedFrame = null;
        SelectedState.Self.SelectedNodes = new System.Collections.Generic.List<object>();
        AppCommands.Self.DoOnUiThread = a => a();
        AppCommands.Self.FileDialogService = NullFileDialogService.Instance;
        AppState.Self.OffsetMultiplier = 1f;
    }

    // ── FormatGuideLabel unit tests ───────────────────────────────────────────

    [Fact]
    public void FormatGuideLabel_Horizontal_ReturnsYPrefix()
    {
        Assert.Equal("Y: 42", PreviewControl.FormatGuideLabel(isHorizontal: true, worldValue: 42f));
    }

    [Fact]
    public void FormatGuideLabel_Vertical_ReturnsXPrefix()
    {
        Assert.Equal("X: -15", PreviewControl.FormatGuideLabel(isHorizontal: false, worldValue: -15f));
    }

    [Fact]
    public void FormatGuideLabel_Zero_ShowsZero()
    {
        Assert.Equal("Y: 0", PreviewControl.FormatGuideLabel(isHorizontal: true, worldValue: 0f));
        Assert.Equal("X: 0", PreviewControl.FormatGuideLabel(isHorizontal: false, worldValue: 0f));
    }

    [Fact]
    public void FormatGuideLabel_FractionalValue_Rounds()
    {
        Assert.Equal("Y: 3", PreviewControl.FormatGuideLabel(isHorizontal: true, worldValue: 2.6f));
        Assert.Equal("X: -2", PreviewControl.FormatGuideLabel(isHorizontal: false, worldValue: -1.6f));
    }

    // ── Visual tests: label appears only while dragging ───────────────────────

    /// <summary>
    /// Rendering a horizontal guide in drag state should produce a bitmap that
    /// differs from the same guide without drag state at the label position.
    /// The label is drawn near (RulerSize+4, sy) where sy is the guide screen Y.
    /// For a 200×200 bitmap with default pan/zoom, worldY=0 → sy = 110, label ≈ y 97–110.
    /// </summary>
    [AvaloniaFact]
    public void HGuide_WhenDragged_LabelPixelsDifferFromNonDragged()
    {
        ResetSingletons();
        const int size = 200;
        var ctrl = new PreviewControl();
        ctrl.SimulateAddHGuide(screenY: 110f, controlHeight: size); // worldY ≈ 0

        // Render without drag (baseline)
        using var bmBase = ctrl.RenderToBitmap(size, size);

        // Render with drag active
        ctrl.SimulateBeginGuideDrag(isHorizontal: true, idx: 0);
        using var bmDrag = ctrl.RenderToBitmap(size, size);

        ctrl.SimulateEndGuideDrag();

        // Label region: x ∈ [21, 80], y ∈ [97, 113] (around the guide line near left edge)
        bool anyDiff = false;
        for (int x = 21; x < 80 && !anyDiff; x++)
            for (int y = 97; y < 114 && !anyDiff; y++)
                anyDiff = bmBase.GetPixel(x, y) != bmDrag.GetPixel(x, y);

        Assert.True(anyDiff, "Dragging a horizontal guide should render a value label (pixel difference expected in label region)");
    }

    /// <summary>
    /// Rendering a vertical guide in drag state should produce a bitmap that
    /// differs from the same guide without drag state at the label position.
    /// For a 200×200 bitmap with default pan/zoom, worldX=0 → sx = 110, label ≈ x 114–160, y 20–34.
    /// </summary>
    [AvaloniaFact]
    public void VGuide_WhenDragged_LabelPixelsDifferFromNonDragged()
    {
        ResetSingletons();
        const int size = 200;
        var ctrl = new PreviewControl();
        ctrl.SimulateAddVGuide(screenX: 110f, controlWidth: size); // worldX ≈ 0

        // Render without drag (baseline)
        using var bmBase = ctrl.RenderToBitmap(size, size);

        // Render with drag active
        ctrl.SimulateBeginGuideDrag(isHorizontal: false, idx: 0);
        using var bmDrag = ctrl.RenderToBitmap(size, size);

        ctrl.SimulateEndGuideDrag();

        // Label region: x ∈ [114, 160], y ∈ [20, 35] (just below top ruler, after guide)
        bool anyDiff = false;
        for (int x = 114; x < 160 && !anyDiff; x++)
            for (int y = 20; y < 36 && !anyDiff; y++)
                anyDiff = bmBase.GetPixel(x, y) != bmDrag.GetPixel(x, y);

        Assert.True(anyDiff, "Dragging a vertical guide should render a value label (pixel difference expected in label region)");
    }

    /// <summary>
    /// After ending drag (SimulateEndGuideDrag), the bitmap should match the
    /// pre-drag baseline exactly in the label region.
    /// </summary>
    [AvaloniaFact]
    public void HGuide_AfterDragEnds_LabelDisappears()
    {
        ResetSingletons();
        const int size = 200;
        var ctrl = new PreviewControl();
        ctrl.SimulateAddHGuide(screenY: 110f, controlHeight: size);

        using var bmBase = ctrl.RenderToBitmap(size, size);

        ctrl.SimulateBeginGuideDrag(isHorizontal: true, idx: 0);
        ctrl.SimulateEndGuideDrag();

        using var bmAfter = ctrl.RenderToBitmap(size, size);

        bool anyDiff = false;
        for (int x = 21; x < 80 && !anyDiff; x++)
            for (int y = 97; y < 114 && !anyDiff; y++)
                anyDiff = bmBase.GetPixel(x, y) != bmAfter.GetPixel(x, y);

        Assert.False(anyDiff, "Label region should match baseline after drag ends");
    }
}
