using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Selection-outline reveal (#542): one-shot <see cref="AnimationEditor.Core.Rendering.RevealAnimation"/>
/// shrink-to-rest when the selected frame set changes — same curve as the PNG diff boxes (#606).
/// Pure easing is covered in <c>RevealAnimationTests</c>; these cover the WireframeControl wiring.
/// </summary>
public class WireframeSelectionRevealTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name = "sprite.png")
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(64, 64);
        bm.Erase(SKColors.DarkGray);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static (WireframeControl ctrl, AnimationFrameSave f0, AnimationFrameSave f1, string dir)
        BuildTwoFrameCtrl(TestServices ctx)
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir);

        var f0 = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0f, TopCoordinate = 0f,
            RightCoordinate = 0.5f, BottomCoordinate = 1f,
            FrameLength = 0.1f, ShapesSave = new ShapesSave()
        };
        var f1 = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0.5f, TopCoordinate = 0f,
            RightCoordinate = 1f, BottomCoordinate = 1f,
            FrameLength = 0.1f, ShapesSave = new ShapesSave()
        };

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.AddRange(new[] { f0, f1 });
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedFrame = f0;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();
        Dispatcher.UIThread.RunJobs();
        ctrl.SettleSelectionReveal();

        return (ctrl, f0, f1, dir);
    }

    [AvaloniaFact]
    public void RefreshFrames_WithoutSelectionChange_DoesNotRestartReveal()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, _, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            Assert.False(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(1f, ctrl.SelectionRevealProgress);

            ctrl.RefreshFrames();

            Assert.False(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(1f, ctrl.SelectionRevealProgress);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SelectedFrame_Change_StartsSelectionReveal()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            Assert.False(ctrl.IsSelectionRevealAnimating);

            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            Assert.True(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(0f, ctrl.SelectionRevealProgress);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SettleSelectionReveal_LandsAtRestAndStops()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctrl.IsSelectionRevealAnimating);

            ctrl.SettleSelectionReveal();

            Assert.False(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(1f, ctrl.SelectionRevealProgress);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void StepSelectionReveal_OneTick_MovesTowardRestWithoutFinishing()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            float before = ctrl.SelectionRevealProgress;
            ctrl.StepSelectionReveal(0.016f);
            float after = ctrl.SelectionRevealProgress;

            Assert.True(ctrl.IsSelectionRevealAnimating);
            Assert.True(after > before && after < 1f,
                $"expected progress in ({before}, 1) after one tick, got {after}");
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Resize-handle fade-in overlaps the tail of the frame-box shrink (#716 follow-up) ───────

    [AvaloniaFact]
    public void HandleFadeProgress_BeforeStartFraction_IsZero()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0f, ctrl.HandleFadeProgress);

            ctrl.StepSelectionReveal(0.016f);

            Assert.True(ctrl.SelectionRevealProgress < RevealAnimation.HandleFadeStartFraction,
                "precondition: still well before the fade's start fraction");
            Assert.True(ctrl.HandleFadeProgress == 0f,
                "Handles must stay invisible for the early part of the shrink, while the box is " +
                "still visibly larger than its final size.");
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// easeOutCubic decelerates hard near progress=1, so the box is already visually at rest well
    /// before the frame-box reveal numerically settles. The handle fade starts partway through
    /// the *same* progress timeline (see RevealAnimation.HandleAlpha) instead of waiting for full
    /// settle, so it lands exactly when the shrink does instead of after a dead pause.
    /// </summary>
    [AvaloniaFact]
    public void HandleFadeProgress_RampsDuringTailOfShrink_AndFinishesExactlyWithIt()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            // Step until the frame box crosses the fade's start fraction, before it settles.
            for (int i = 0; i < 61 && ctrl.SelectionRevealProgress < RevealAnimation.HandleFadeStartFraction; i++)
                ctrl.StepSelectionReveal(1f / 60f);

            Assert.True(ctrl.SelectionRevealProgress is >= RevealAnimation.HandleFadeStartFraction and < 1f,
                $"precondition: past the start fraction but not yet settled, got {ctrl.SelectionRevealProgress}");
            Assert.True(ctrl.HandleFadeProgress > 0f && ctrl.HandleFadeProgress < 1f,
                $"expected the handle fade under way while the frame box is still finishing its shrink, got {ctrl.HandleFadeProgress}");

            ctrl.SettleSelectionReveal();

            Assert.Equal(1f, ctrl.SelectionRevealProgress);
            Assert.Equal(1f, ctrl.HandleFadeProgress);
            Assert.False(ctrl.IsSelectionRevealAnimating);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SettleSelectionReveal_LeavesHandlesFullyVisible()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            ctrl.SettleSelectionReveal();

            Assert.Equal(1f, ctrl.SelectionRevealProgress);
            Assert.Equal(1f, ctrl.HandleFadeProgress);
            Assert.False(ctrl.IsSelectionRevealAnimating);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Whole-chain selection also highlights + reveals every frame (#716 follow-up) ──────────

    /// <summary>
    /// Selecting a chain as a whole (the ordinary single-click a tree row already does — no
    /// double-click, no explicit multi-frame selection) must highlight and reveal every one of
    /// its frames, the same pulse a single-frame selection gets. Double-click is reserved for
    /// moving the camera onto them (see MainWindow.HandleAnimTreeNodeDoubleTap).
    /// </summary>
    [AvaloniaFact]
    public void SelectedChain_Change_HighlightsAllFramesAndStartsReveal()
    {
        var ctx = ResetSingletons();
        var (ctrl, f0, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            // BuildTwoFrameCtrl leaves SelectedFrame = f0; selecting the chain as a whole must
            // supersede that single-frame selection and light up both frames.
            ctx.SelectedState.SelectedChain = null;
            ctx.SelectedState.SelectedChain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];
            Dispatcher.UIThread.RunJobs();

            Assert.True(ctrl.IsSelectionRevealAnimating,
                "Selecting a whole chain must restart the shrink-to-rest reveal.");

            var rects = ctrl.GetFrameRects();
            Assert.Equal(2, rects.Count);
            Assert.All(rects, r => Assert.True(r.IsSelected,
                "Every frame of a whole-chain selection must draw with the blue highlight."));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression: a single-frame selection shows (and highlights) only that frame, not its
    /// siblings — unchanged from before the whole-chain-selection behavior was added.
    /// </summary>
    [AvaloniaFact]
    public void SelectedFrame_WithinChain_ShowsOnlyThatFrameSelected()
    {
        var ctx = ResetSingletons();
        var (ctrl, f0, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            var rects = ctrl.GetFrameRects();
            Assert.Single(rects);
            Assert.True(rects[0].IsSelected);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Per-frame reveal: only newly-added frames replay (#1027) ──────────────────────────────

    /// <summary>
    /// Extending a multi-frame selection (e.g. Shift+Arrow, #1023) from [f0] to [f0, f1] must
    /// only play the reveal on the newly-added frame f1 — f0 was already settled and must stay
    /// that way, not replay just because the highlighted *set* changed.
    /// </summary>
    [AvaloniaFact]
    public void ExtendingMultiFrameSelection_OnlyNewFrameReplaysReveal()
    {
        var ctx = ResetSingletons();
        var (ctrl, f0, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            // BuildTwoFrameCtrl's initial SelectedFrame=f0 assignment happens before the control
            // subscribes to SelectionChanged, so f0 was never actually tracked/revealed yet.
            // Re-assign it (the setter always fires SelectionChanged, even for the same value) to
            // establish it as a real, settled reveal before testing the extension below.
            ctx.SelectedState.SelectedFrame = f0;
            Dispatcher.UIThread.RunJobs();
            ctrl.SettleSelectionReveal();
            Assert.Equal(1f, ctrl.GetSelectionRevealProgress(f0));
            Assert.False(ctrl.IsSelectionRevealAnimating);

            // Real Shift+Arrow extension (#1023) syncs the tree's multi-selection into
            // SelectedNodes, which is what ComputeHighlightedFrames actually reads once more
            // than one frame is selected (SelectedFrame itself may also get reassigned to
            // whichever row is now the range's focused end, but that doesn't change the
            // resulting highlighted set here).
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            Dispatcher.UIThread.RunJobs();

            Assert.True(ctrl.IsSelectionRevealAnimating,
                "Extending the selection must start the reveal for the newly-added frame.");
            Assert.Equal(1f, ctrl.GetSelectionRevealProgress(f0));
            Assert.Equal(0f, ctrl.GetSelectionRevealProgress(f1));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Shrinking a multi-frame selection back down must drop the removed frame's reveal state
    /// entirely (no lingering timer/host) while leaving the still-selected frame untouched.
    /// </summary>
    [AvaloniaFact]
    public void ShrinkingMultiFrameSelection_DropsRemovedFramesRevealState()
    {
        var ctx = ResetSingletons();
        var (ctrl, f0, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            Dispatcher.UIThread.RunJobs();
            ctrl.SettleSelectionReveal();

            // Real single-selection narrowing (e.g. Shift+Up back down to one row) syncs both
            // SelectedNodes and SelectedFrame to the sole remaining row (MainWindow.OnTreeSelectionChanged
            // always does both, via RouteNodeSelection).
            ctx.SelectedState.SelectedNodes = new List<object> { f0 };
            ctx.SelectedState.SelectedFrame = f0;
            Dispatcher.UIThread.RunJobs();

            Assert.False(ctrl.IsSelectionRevealAnimating,
                "Shrinking the selection must not replay the reveal on the still-selected frame.");
            Assert.Equal(1f, ctrl.GetSelectionRevealProgress(f0));
            Assert.True(ctrl.GetSelectionRevealProgress(f1) == 1f,
                "A frame no longer highlighted has no reveal state, which reads as settled (1f).");
        }
        finally { Directory.Delete(dir, true); }
    }
}
