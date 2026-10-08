using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The highlighted polygon vertex (#1258) is drawn larger than the others. On a 64x64 canvas the
/// world origin lands at (42,42) (see <see cref="PreviewLockedChainHandleRenderingTests"/>), so the
/// vertex at (8,8) centres on (50,34); pixel (54,34) is inside the enlarged square only.
/// </summary>
public class PreviewPolygonVertexHighlightRenderingTests
{
    [AvaloniaFact]
    public void RenderToBitmap_InspectorVertexIndexSet_DrawsThatVertexEnlarged()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.AppState.OffsetMultiplier = 1f;
        var polygon = new PolygonSave();
        foreach (var (x, y) in new[] { (-8f, -8f), (8f, -8f), (8f, 8f), (-8f, 8f), (-8f, -8f) })
            polygon.Points.Add(new Vector2Save { X = x, Y = y });
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Add(polygon);
        var chain = new AnimationChainSave { Name = "Test" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedFrame   = frame;
        ctx.SelectedState.SelectedPolygon = polygon;
        var ctrl = ctx.CreatePreviewControl();

        using (var before = ctrl.RenderToBitmap(64, 64))
            before.GetPixel(54, 34).Red.ShouldBeLessThan((byte)100);

        ctrl.InspectorVertexIndex = 2;
        using var after = ctrl.RenderToBitmap(64, 64);

        var px = after.GetPixel(54, 34);
        (px.Red > 200 && px.Green > 200 && px.Blue > 200).ShouldBeTrue($"R={px.Red} G={px.Green} B={px.Blue}");
    }
}
