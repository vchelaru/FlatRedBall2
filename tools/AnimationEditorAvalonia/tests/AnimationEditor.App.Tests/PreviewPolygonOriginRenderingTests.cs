using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A selected polygon draws a crosshair at its own X/Y (#1352), since its vertices are relative to
/// that point. On a 64x64 canvas the world origin lands at (42,42), so a polygon at X=10 has its
/// origin at (52,42); pixel (52,38) is on the crosshair's vertical arm and clear of the outline.
/// </summary>
public class PreviewPolygonOriginRenderingTests
{
    [AvaloniaFact]
    public void RenderToBitmap_PolygonSelected_DrawsCrosshairAtPolygonOrigin()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.AppState.OffsetMultiplier = 1f;
        var polygon = new PolygonSave { X = 10, Y = 0 };
        foreach (var (x, y) in new[] { (-8f, -8f), (8f, -8f), (8f, 8f), (-8f, 8f), (-8f, -8f) })
            polygon.Points.Add(new Vector2Save { X = x, Y = y });
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Add(polygon);
        var chain = new AnimationChainSave { Name = "Test" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedFrame = frame;
        var ctrl = ctx.CreatePreviewControl();

        using (var unselected = ctrl.RenderToBitmap(64, 64))
            unselected.GetPixel(52, 38).Red.ShouldBeLessThan((byte)100);

        ctx.SelectedState.SelectedPolygon = polygon;
        using var selected = ctrl.RenderToBitmap(64, 64);

        var px = selected.GetPixel(52, 38);
        (px.Red > 200 && px.Green > 150 && px.Blue < 100).ShouldBeTrue($"R={px.Red} G={px.Green} B={px.Blue}");
    }
}
