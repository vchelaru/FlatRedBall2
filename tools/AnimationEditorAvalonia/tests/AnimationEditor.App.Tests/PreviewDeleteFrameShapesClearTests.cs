using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that collision-shape overlays are cleared from the preview after a
/// frame with shapes is deleted. Issue #284.
/// </summary>
public class PreviewDeleteFrameShapesClearTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AnimationFrameSave MakeFrameWithRect(float scaleX = 5f, float scaleY = 5f)
    {
        var frame = new AnimationFrameSave { FrameLength = 0.1f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(new AARectSave { ScaleX = scaleX, ScaleY = scaleY });
        return frame;
    }

    /// <summary>
    /// Registers the chain in the ProjectManager's ACLS so that SelectedState.SelectedFrame
    /// can auto-discover the parent chain via FindChainForFrame. Required when pinning a
    /// frame before calling AppCommands.DeleteFrames.
    /// </summary>
    private static void RegisterChain(TestServices ctx, AnimationChainSave chain)
    {
        var acls = ctx.ProjectManager.AnimationChainListSave ?? new AnimationChainListSave();
        if (!acls.AnimationChains.Contains(chain))
            acls.AnimationChains.Add(chain);
        ctx.ProjectManager.AnimationChainListSave = acls;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the only frame in a chain has shapes and is deleted, the preview should
    /// show no shapes after the delete.
    /// </summary>
    [AvaloniaFact]
    public void DeletePinnedFrame_OnlyFrameWithShapes_ClearsPreviewShapes()
    {
        var ctx   = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Test" };
        var frame = MakeFrameWithRect();
        chain.Frames.Add(frame);
        RegisterChain(ctx, chain);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(ctrl.GetShapeInfosForTest()); // baseline: shapes visible before delete

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame });
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(ctrl.GetShapeInfosForTest());
    }

    /// <summary>
    /// When the first of two frames (both with shapes) is deleted while pinned,
    /// the preview should not continue to show the deleted frame's shapes.
    /// The remaining frame may be shown, but shapes from the deleted frame must be gone.
    /// </summary>
    [AvaloniaFact]
    public void DeletePinnedFrame_FirstOfTwoFramesWithShapes_DeletedFrameShapesNotShown()
    {
        var ctx    = TestHelpers.BuildServices();
        var chain  = new AnimationChainSave { Name = "Test" };
        var frame0 = MakeFrameWithRect(scaleX: 10f, scaleY: 10f); // unique size to identify
        var frame1 = MakeFrameWithRect(scaleX: 20f, scaleY: 20f);
        chain.Frames.Add(frame0);
        chain.Frames.Add(frame1);
        RegisterChain(ctx, chain);

        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame0;
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(ctrl.GetShapeInfosForTest());

        ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { frame0 });
        Dispatcher.UIThread.RunJobs();

        var shapes = ctrl.GetShapeInfosForTest();
        // The deleted frame's shapes (ScaleX=10) must not appear.
        Assert.DoesNotContain(shapes, s => s.Param1 == 10f);
    }
}
