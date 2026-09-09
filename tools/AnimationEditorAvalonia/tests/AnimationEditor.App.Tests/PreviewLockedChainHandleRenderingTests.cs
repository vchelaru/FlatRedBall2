using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A locked chain's selected shape must show no resize handles in the Preview panel (#1032
/// follow-up) -- the drag itself is already guarded (see <see cref="PreviewLockedChainDragTests"/>),
/// but the little squares must not imply the shape is resizable. The gold "selected" highlight is
/// still shown (unaffected -- <see cref="PreviewControl.GetShapeInfosForTest"/>'s <c>IsSelected</c>
/// keeps driving that), only the handle squares are suppressed.
/// </summary>
public class PreviewLockedChainHandleRenderingTests
{
    /// <summary>
    /// A 10x10-half-extent rect at world origin renders its screen body at (32,32,52,52) on a
    /// 64x64 canvas (RulerSize=20, offsetMultiplier=zoom=1). Its TopLeft handle -- drawn 5px
    /// outside the body -- would be centred at (27,27), a white square when rendered.
    /// </summary>
    private static (PreviewControl ctrl, AARectSave rect) BuildCtrlWithSelectedRect(bool locked)
    {
        var ctx = TestHelpers.BuildServices();
        ctx.AppState.OffsetMultiplier = 1f;

        var rect  = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Shapes.Add(rect);
        var chain = new AnimationChainSave { Name = "Test", IsLocked = locked };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedFrame      = frame;
        ctx.SelectedState.SelectedRectangle  = rect;

        return (ctx.CreatePreviewControl(), rect);
    }

    [AvaloniaFact]
    public void RenderToBitmap_SelectedShapeInLockedChain_NoHandleRendered()
    {
        var (ctrl, _) = BuildCtrlWithSelectedRect(locked: true);

        using var bm = ctrl.RenderToBitmap(64, 64);

        var px = bm.GetPixel(27, 27);
        Assert.True(px.Red < 100 && px.Green < 100 && px.Blue < 100,
            $"Locked chain's selected shape should render no handle; R={px.Red} G={px.Green} B={px.Blue}");
    }

    [AvaloniaFact]
    public void RenderToBitmap_SelectedShapeInUnlockedChain_HandleRendered()
    {
        var (ctrl, _) = BuildCtrlWithSelectedRect(locked: false);

        using var bm = ctrl.RenderToBitmap(64, 64);

        var px = bm.GetPixel(27, 27);
        Assert.True(px.Red > 200 && px.Green > 200 && px.Blue > 200,
            $"Unlocked chain's selected shape should render its TopLeft handle; R={px.Red} G={px.Green} B={px.Blue}");
    }
}
