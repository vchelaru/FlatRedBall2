using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for WireframeControl zoom and pan centering behaviour when a
/// ScrollViewer is attached (as in production via MainWindow).
///
/// Key behaviour under test:
///   • After loading a small texture the image is centred (PanX &gt; 0).
///   • A zoom-in that keeps the image inside the viewport must NOT reset PanX
///     to zero — the "image jumps to top-left corner on first zoom" bug.
///   • A large zoom-in that overflows the viewport enters scroll mode (PanX = 0,
///     ScrollViewer offset &gt; 0 or image fills viewport).
///   • Zooming back out from scroll mode restores free-pan centering (PanX &gt; 0).
/// </summary>
public class WireframePanZoomTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TestServices ResetSingletons() {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name, int size = 32)
        => WriteSolidPng(dir, name, size, size);

    private static string WriteSolidPng(string dir, string name, int width, int height)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(width, height);
        bm.Erase(SKColors.Red);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    /// <summary>
    /// Asserts the core ZoomToward post-conditions that must hold after ANY zoom operation:
    /// <list type="bullet">
    ///   <item><c>panX/Y ≥ 0</c> — sprite not pushed off the left/top edge (#319).</item>
    ///   <item>Sprite centre reachable by scrolling:
    ///         <c>panX + imgW×zoom/2 ≥ vpW/2</c> — centering never locked (#341).</item>
    /// </list>
    /// These two invariants subsume every zoom bug we have fixed; a negative panX
    /// violates the first, a too-small panX violates the second.
    /// </summary>
    private static void AssertZoomInvariants(WireframeControl ctrl, ScrollViewer sv, string context = "")
    {
        var (panX, panY, zoom) = ctrl.CameraState;
        var (imgW, imgH)       = ctrl.BitmapSize;
        float vpW = (float)sv.Viewport.Width;
        float vpH = (float)sv.Viewport.Height;

        Assert.True(panX >= 0f,
            $"{context}: panX={panX:F1} < 0 — sprite pushed off the left edge " +
            $"(zoom={zoom:F3}, vpW={vpW:F1}, imgW={imgW})");
        Assert.True(panY >= 0f,
            $"{context}: panY={panY:F1} < 0 — sprite pushed above the top edge " +
            $"(zoom={zoom:F3}, vpH={vpH:F1}, imgH={imgH})");

        float centreScrollX = panX + imgW * zoom / 2f - vpW / 2f;
        float centreScrollY = panY + imgH * zoom / 2f - vpH / 2f;
        Assert.True(centreScrollX >= 0f,
            $"{context}: centreScrollX={centreScrollX:F1} < 0 — " +
            $"sprite centre unreachable by scrolling (panX={panX:F1}, imgW={imgW}, zoom={zoom:F3}, vpW={vpW:F1})");
        Assert.True(centreScrollY >= 0f,
            $"{context}: centreScrollY={centreScrollY:F1} < 0 — " +
            $"sprite centre unreachable by scrolling (panY={panY:F1}, imgH={imgH}, zoom={zoom:F3}, vpH={vpH:F1})");
    }

    // ── LoadTexture centres small images ──────────────────────────────────────

    /// <summary>
    /// After loading a small texture into a MainWindow wireframe (which has a
    /// ScrollViewer attached), the image must be centred: PanX &gt; 0.
    /// </summary>
    [AvaloniaFact]
    public void LoadTexture_SmallImage_IsCentred_PanXGreaterThanZero()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "small.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();  // settle initial layout

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();  // flush CenterTexture scroll-reset post

            var (panX, panY, _) = ctrl.CameraState;
            Assert.True(panX > 0,
                $"Small image must be centred after LoadTexture; PanX={panX}");
            Assert.True(panY > 0,
                $"Small image must be centred after LoadTexture; PanY={panY}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Zoom-in (image still fits) keeps image centred ────────────────────────

    /// <summary>
    /// Zooming in slightly on a small image that still fits the viewport must
    /// keep PanX &gt; 0.  This is the primary regression guard for the bug where
    /// ZoomToward always reset _panX to zero when a ScrollViewer was attached.
    /// </summary>
    [AvaloniaFact]
    public void SetZoomPercent_SlightZoomIn_SmallImageStayCentred_PanXGreaterThanZero()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "small.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();  // flush CenterTexture scroll-reset post

            var (panXBefore, _, _) = ctrl.CameraState;

            // Zoom to 110% of current zoom (slight zoom in — image should still fit)
            int currentZoomPct = (int)(ctrl.CameraState.Zoom * 100f);
            ctrl.SetZoomPercent((int)(currentZoomPct * 1.1f));
            Dispatcher.UIThread.RunJobs();

            var (panXAfter, panYAfter, _) = ctrl.CameraState;
            Assert.True(panXAfter > 0,
                $"After slight zoom-in on small image PanX must remain positive (image stays centred); " +
                $"PanXBefore={panXBefore:F1} PanXAfter={panXAfter:F1}");
            Assert.True(panYAfter > 0,
                $"After slight zoom-in on small image PanY must remain positive; PanYAfter={panYAfter:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Zoom-out from scroll mode restores free-pan centering ─────────────────

    /// <summary>
    /// After zooming in to 100 % (1:1) so the 32×32 image is definitely smaller
    /// than the viewport, then zooming out further to 50 %, the image must still
    /// be centred (PanX &gt; 0), not stuck at the top-left corner.
    ///
    /// This guards the scroll→free-pan transition in ZoomToward where the fix
    /// subtracts the previous scroll offset from newPanX.
    /// </summary>
    [AvaloniaFact]
    public void SetZoomPercent_ZoomOut_SmallImageStayCentred_PanXGreaterThanZero()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "small.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Zoom to 100% then back down to 50% — image should stay centred
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            ctrl.SetZoomPercent(50);
            Dispatcher.UIThread.RunJobs();

            var (panX, panY, _) = ctrl.CameraState;
            Assert.True(panX > 0,
                $"After zoom-out to 50% on small image PanX must be positive; PanX={panX:F1}");
            Assert.True(panY > 0,
                $"After zoom-out to 50% on small image PanY must be positive; PanY={panY:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Scroll mode: image has dead-space padding ─────────────────────────────

    /// <summary>
    /// After zooming in far enough that the image overflows the viewport, the
    /// image must NOT be placed at content origin (0,0). PanX must be > 0 (= PanPadding)
    /// so that the ScrollViewer content area has dead space on every side,
    /// allowing the user to pan beyond the image boundary.
    ///
    /// A 32×32 image at 3200 % = 1024 px wide — this should overflow the headless
    /// window's viewport and trigger scroll mode.
    ///
    /// This is the regression guard for bug #8: "locked from panning into dead space".
    /// </summary>
    [AvaloniaFact]
    public void SetZoomPercent_LargeZoom_ImageInScrollMode_PanXEqualsDeadSpacePadding()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "small.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // 3200 % → 32 × 32 = 1024 px; should overflow the viewport and enter scroll mode.
            ctrl.SetZoomPercent(3200);
            Dispatcher.UIThread.RunJobs();

            var (panX, panY, zoom) = ctrl.CameraState;

            // In scroll mode panX/panY must be PanPadding (> 0), NOT 0.
            // PanPadding > 0 means there is content to the left/above the image
            // that the user can scroll into.
            Assert.True(panX > 0,
                $"In scroll mode PanX must be > 0 (dead-space padding); PanX={panX:F1}, Zoom={zoom:F2}");
            Assert.True(panY > 0,
                $"In scroll mode PanY must be > 0 (dead-space padding); PanY={panY:F1}, Zoom={zoom:F2}");

            // Content width must exceed imageW to include padding on both sides.
            double imageW = 32 * zoom;
            Assert.True(ctrl.Bounds.Width > imageW,
                $"Content width ({ctrl.Bounds.Width:F1}) must exceed imageW ({imageW:F1}) to hold dead-space padding");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Scroll-pan correctness ────────────────────────────────────────────────

    /// <summary>
    /// Regression guard for the "crazy stutter" bug (pan coordinate-space issue).
    ///
    /// In scroll mode, dragging the viewport 50 px to the left must increase
    /// the ScrollViewer offset by exactly 50 px.
    ///
    /// The stutter was caused by <c>e.GetPosition(this)</c> returning
    /// <em>content-space</em> coords (viewport + scroll offset) instead of
    /// <em>viewport-space</em> coords. After each frame, the changed offset
    /// shifted content-space position, making the delta oscillate.
    /// The fix uses <c>e.GetPosition(_scrollViewer)</c>.
    /// </summary>
    [AvaloniaFact]
    public void ScrollPan_PanLeft50px_ScrollOffsetIncreasesBy50()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "big.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Zoom in until we're in scroll mode (image overflows viewport).
            ctrl.SetZoomPercent(3200);
            Dispatcher.UIThread.RunJobs();

            double initialX = sv.Offset.X;

            // Drag viewport 50 px to the left → scroll should move right by 50.
            ctrl.SimulatePanStart(200f, 100f);
            ctrl.SimulatePanMove(150f, 100f);   // delta = -50 → scrollX += 50
            ctrl.SimulatePanEnd();
            Dispatcher.UIThread.RunJobs();

            double newX = sv.Offset.X;
            Assert.True(Math.Abs((newX - initialX) - 50.0) < 2.0,
                $"Panning 50 px left should increase scroll offset by 50; " +
                $"initialX={initialX:F1} newX={newX:F1} delta={newX - initialX:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Scroll-pan stability (oscillation regression) ─────────────────────────

    /// <summary>
    /// Regression guard for the pan-stutter oscillation bug.
    ///
    /// With the <em>buggy</em> code, calling <see cref="WireframeControl.SimulatePanMove"/>
    /// twice with the <em>same endpoint</em> would produce different scroll offsets on each
    /// call because the content-space position drifted when the offset changed.
    ///
    /// With the fix, <see cref="WireframeControl.SimulatePanMove"/> is idempotent for the
    /// same endpoint: calling it multiple times while the mouse stays still must not change
    /// the scroll offset (no oscillation).
    /// </summary>
    [AvaloniaFact]
    public void ScrollPan_SameEndpoint_RepeatedMove_ScrollIsStable_NoOscillation()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "big.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            ctrl.SetZoomPercent(3200);
            Dispatcher.UIThread.RunJobs();

            // Start a pan, move to a fixed viewport position once…
            ctrl.SimulatePanStart(200f, 100f);
            ctrl.SimulatePanMove(150f, 100f);
            double scrollAfterMove1 = sv.Offset.X;

            // …then "move" to the exact same position again (mouse didn't move —
            // simulates the second render frame in the old buggy loop).
            ctrl.SimulatePanMove(150f, 100f);
            double scrollAfterMove2 = sv.Offset.X;

            ctrl.SimulatePanEnd();

            Assert.True(Math.Abs(scrollAfterMove1 - scrollAfterMove2) < 1.0,
                $"Same-endpoint repeated move must not change scroll offset (oscillation guard); " +
                $"move1={scrollAfterMove1:F1} move2={scrollAfterMove2:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Rapid-zoom pan-lock regression ────────────────────────────────────────

    /// <summary>
    /// Regression guard for "pan lock between 100-400% zoom" caused by stale scroll
    /// offsets during rapid zooming.
    ///
    /// Root cause: <c>ZoomToward</c> used to read <c>_scrollViewer.Offset</c> which
    /// is set via a deferred <c>Dispatcher.Post</c>.  When the user spins the mouse
    /// wheel fast (multiple events before any Post fires), each event reads the
    /// still-zero (or unchanged) offset and computes a near-zero <c>newScrollX</c>.
    /// The last queued Post then locks the scroll near 0, making rightward pan
    /// effectively impossible.
    ///
    /// Fix: a synchronous <c>_scrollTargetX/Y</c> field is updated in
    /// <c>ZoomToward</c> before queuing the Post so every subsequent zoom event
    /// chains off the correctly-accumulated target.
    /// </summary>
    [AvaloniaFact]
    public void RapidZoom_ScrollTargetIsCorrect_PanNotLocked()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // A 256×256 image overflows any headless viewport at 500%+ zoom.
            var texPath = WriteSolidPng(dir, "medium.png", size: 256);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // ── Rapid zoom: three SetZoomPercent calls WITHOUT flushing between them ──
            // Simulates spinning the mouse wheel fast so each Post is queued before
            // the previous one has fired.  Without the fix, every ZoomToward reads
            // _scrollViewer.Offset == 0 (stale), computes newScrollX ≈ 0, and the
            // final scroll lands near 0, locking rightward pan.
            ctrl.SetZoomPercent(500);
            ctrl.SetZoomPercent(1000);
            ctrl.SetZoomPercent(2000);

            // Flush all queued Dispatcher.Posts so _scrollViewer.Offset is finalised.
            Dispatcher.UIThread.RunJobs();

            double scroll0 = sv.Offset.X;

            // With the bug: scroll0 ≈ 0 (stale chain).
            // With the fix: scroll0 is large (correctly accumulated targets).
            // 200 px is a conservative lower bound; the correct value is several thousand.
            Assert.True(scroll0 > 200,
                $"After rapid zoom to 2000%, scroll offset should be well above 0 to " +
                $"allow rightward pan; got scroll0={scroll0:F1}. " +
                "Indicates stale scroll accumulation during rapid zoom (pan-lock bug).");

            // Pan right 200 px (dX = +200 → scroll decreases by 200).
            // Fails when scroll0 ≈ 0 because the offset is immediately clamped to 0
            // and the image cannot move.
            const float panAmt = 200f;
            ctrl.SimulatePanStart(400f, 300f);
            ctrl.SimulatePanMove(400f + panAmt, 300f);
            ctrl.SimulatePanEnd();

            double scroll1 = sv.Offset.X;
            Assert.True(Math.Abs((scroll0 - scroll1) - panAmt) < 2.0,
                $"Panning right {panAmt} px should decrease scroll by {panAmt} px; " +
                $"scroll0={scroll0:F1} scroll1={scroll1:F1} moved={scroll0 - scroll1:F1}.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Moderate-zoom pan-lock regression (single zoom to overflow) ───────────

    /// <summary>
    /// Regression guard for pan lock at moderate zoom (150-200%) where the image
    /// JUST crosses the overflow threshold for the first time.
    ///
    /// Repro: load sprite → zoom in toward column 3 to ~150-200% → pan to the right
    /// (drag mouse right) → observe pan lock.
    ///
    /// Root cause hypothesis: when the image crosses the overflow threshold on a
    /// single zoom event, <c>ZoomToward</c> sets <c>_scrollTargetX</c> correctly
    /// (large, non-zero), but <c>ScrollChanged</c> may fire from the layout
    /// Extent-change event with <c>Offset.X = 0</c> (before our deferred Post),
    /// resetting <c>_scrollTargetX</c> to 0.  On the next <c>StartPan</c>,
    /// <c>_scrollAnchorX = 0</c>, and any rightward drag (dX > 0) immediately
    /// clamps to 0 — locked.
    ///
    /// This test verifies both:
    ///   1. After zooming into overflow via wheel zoom toward column 3, the final
    ///      <c>ScrollTarget.X</c> is well above 0.
    ///   2. A rightward pan gesture (dX = +200) decreases the scroll offset by ~200.
    /// </summary>
    [AvaloniaFact]
    public void ModerateZoom_SingleOverflowTransition_PanRightIsNotLocked()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 512×512 image — overflows a typical wireframe viewport at ~150-200% zoom.
            var texPath = WriteSolidPng(dir, "medium512.png", size: 512);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Column 3 of a 4-column grid (128 px cells on a 512 px image) is at
            // texture x = 3 * 128 = 384 px.  Compute its viewport-space position.
            var (panX0, panY0, zoom0) = ctrl.CameraState;
            float col3ViewportX = panX0 + 384f * zoom0;  // texture-to-viewport mapping
            float pivotY        = (float)(sv.Viewport.Height / 2);

            // Simulate 4 wheel-zoom-in notches toward column 3 (1.25^4 ≈ 2.44×).
            // At ≥2× zoom the 512-px image exceeds any typical viewport width
            // (triggering scroll mode).  These are queued WITHOUT RunJobs between
            // them to mimic rapid scrolling.
            for (int i = 0; i < 4; i++)
                ctrl.SimulateWheelZoom(col3ViewportX, pivotY, 1.25f);

            Dispatcher.UIThread.RunJobs();  // flush all deferred Posts + layout

            double scroll0 = sv.Offset.X;
            var (stX, _)   = ctrl.ScrollTarget;

            // ── Assert 1: scroll must be well above 0 ──────────────────────────
            // If the ScrollChanged-from-Extent-change bug fires, scroll0 ≈ 0 here.
            Assert.True(scroll0 > 100,
                $"After zooming into overflow toward column 3, scroll offset should be " +
                $"well above 0 to allow rightward pan; got scroll0={scroll0:F1}. " +
                "Indicates _scrollTargetX was reset to 0 by a spurious ScrollChanged.");

            Assert.True(stX > 100,
                $"ScrollTarget.X should mirror the final scroll; got stX={stX:F1}.");

            // ── Assert 2: pan right (dX = +200) decreases scroll by ~200 ──────
            // Fails if _scrollAnchorX = 0 (pan lock): max(0, 0 - 200) = 0, no motion.
            const float panAmt = 200f;
            ctrl.SimulatePanStart(400f, 300f);
            ctrl.SimulatePanMove(400f + panAmt, 300f);
            ctrl.SimulatePanEnd();

            double scroll1 = sv.Offset.X;
            Assert.True(Math.Abs((scroll0 - scroll1) - panAmt) < 5.0,
                $"Panning right {panAmt} px should decrease scroll by {panAmt} px; " +
                $"scroll0={scroll0:F1} scroll1={scroll1:F1} moved={scroll0 - scroll1:F1}.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression guard: after a single zoom step that crosses the free-pan→scroll
    /// boundary, panning in BOTH directions must work.
    ///
    /// This supplements <see cref="ModerateZoom_SingleOverflowTransition_PanRightIsNotLocked"/>
    /// by also verifying that leftward pan (dX &lt; 0, scroll increases) works, proving
    /// neither direction is locked after the first overflow transition.
    /// </summary>
    [AvaloniaFact]
    public void ModerateZoom_OverflowTransition_PanBothDirectionsWork()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "medium512b.png", size: 512);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Zoom directly to 250% (single call) — well into overflow for 512×512.
            ctrl.SetZoomPercent(250);
            Dispatcher.UIThread.RunJobs();

            double scroll0 = sv.Offset.X;

            // Sanity: must be in scroll mode (scroll > 0 or image overflows)
            Assert.True(ctrl.CameraState.PanX > 0,
                $"Should be in scroll mode (PanX = PanPadding); PanX={ctrl.CameraState.PanX:F1}");

            // Pan RIGHT 100 px (dX = +100 → scroll decreases by 100)
            const float rightAmt = 100f;
            ctrl.SimulatePanStart(300f, 200f);
            ctrl.SimulatePanMove(300f + rightAmt, 200f);
            ctrl.SimulatePanEnd();
            Dispatcher.UIThread.RunJobs();

            double scrollAfterRight = sv.Offset.X;
            Assert.True(Math.Abs((scroll0 - scrollAfterRight) - rightAmt) < 5.0,
                $"Pan right {rightAmt}px should decrease scroll by {rightAmt}; " +
                $"before={scroll0:F1} after={scrollAfterRight:F1} delta={scroll0 - scrollAfterRight:F1}");

            // Pan LEFT 100 px from new position (dX = -100 → scroll increases by 100)
            const float leftAmt = 100f;
            ctrl.SimulatePanStart(300f, 200f);
            ctrl.SimulatePanMove(300f - leftAmt, 200f);
            ctrl.SimulatePanEnd();
            Dispatcher.UIThread.RunJobs();

            double scrollAfterLeft = sv.Offset.X;
            Assert.True(Math.Abs((scrollAfterLeft - scrollAfterRight) - leftAmt) < 5.0,
                $"Pan left {leftAmt}px should increase scroll by {leftAmt}; " +
                $"before={scrollAfterRight:F1} after={scrollAfterLeft:F1} delta={scrollAfterLeft - scrollAfterRight:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// White-box regression test for the live-app pan-lock race condition.
    ///
    /// Root cause: in Avalonia's live render loop, Render priority (7) runs BEFORE
    /// Layout priority (5).  The old code used <c>Dispatcher.Post(Render, …)</c> to
    /// apply the zoom scroll, which fired BEFORE the layout pass.  At that moment the
    /// content still had its old (smaller) size, so the offset was clamped to 0.
    /// <c>ScrollChanged</c> then fired with <c>Offset.X = 0</c>, resetting
    /// <c>_scrollTargetX</c> to 0 and locking panning.
    ///
    /// A second attempt used <c>LayoutUpdated</c> to defer the apply, but
    /// <c>Control.LayoutUpdated</c> in Avalonia fires after ANY layout pass in the
    /// window (global, not per-control).  If an unrelated control (e.g. the scrollbar
    /// becoming visible) triggers a layout pass before WireframeControl is re-measured,
    /// <c>ApplyPendingScroll</c> fires on old content → offset clamped to 0 →
    /// handler unregistered → WireframeControl's own layout runs later but no handler
    /// remains → scroll stays 0 → pan locked.
    ///
    /// The current fix queues <c>Post(ApplyPendingScrollIfNeeded, Render)</c> from
    /// <em>inside</em> <c>MeasureOverride</c>.  A Post enqueued during a layout pass
    /// can only be dispatched <em>after</em> that layout pass returns, so the content
    /// is guaranteed to have the new (larger) size when the offset is applied.
    ///
    /// This test directly injects the intermediate <c>ScrollChanged(Offset=0)</c>
    /// race step between the zoom call and <c>RunJobs</c>:
    /// <list type="number">
    ///   <item>Zoom into overflow → <c>_pendingScrollX = X &gt; 0</c></item>
    ///   <item>Inject race: set <c>sv.Offset = 0</c> → <c>ScrollChanged</c> → <c>_scrollTargetX = 0</c></item>
    ///   <item>RunJobs → layout grows content → MeasureOverride queues Post → Post applies <c>_pendingScrollX</c></item>
    ///   <item>Assert final scroll is non-zero and pan is not locked</item>
    /// </list>
    /// </summary>
    [AvaloniaFact]
    public void ZoomOverflow_IntermediateScrollChangedZero_PanRemainsUnlocked()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "race512.png", size: 512);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();  // centred, scroll = 0, not in overflow

            // ── Step 1: zoom to 250% — enters overflow, queues layout + pending scroll.
            //    DO NOT call RunJobs yet so _pendingScrollApply is still true.
            ctrl.SetZoomPercent(250);

            double pendingX = ctrl.ScrollTarget.X;
            Assert.True(pendingX > 100,
                $"After SetZoomPercent(250) the pending scroll target should be well " +
                $"above 0; got {pendingX:F1}.  Was _zoom not updated or overflowX false?");

            // ── Step 2: inject the race — simulate the live-app scenario where
            //    ScrollChanged(Offset=0) fires from the content-Extent change BEFORE
            //    our LayoutUpdated handler applies the pending offset.
            sv.Offset = new Vector(0, 0);   // fires ScrollChanged → _scrollTargetX = 0

            // ── Step 3: flush — layout runs, LayoutUpdated fires, ApplyPendingScroll
            //    reads _pendingScrollX (not _scrollTargetX which was reset to 0).
            Dispatcher.UIThread.RunJobs();

            double finalScrollX = sv.Offset.X;
            var (stX, _) = ctrl.ScrollTarget;

            // ── Assert A: scroll must have been restored to the intended non-zero value.
            Assert.True(finalScrollX > 100,
                $"After RunJobs, scroll should be restored to ~{pendingX:F0} by " +
                $"ApplyPendingScroll; got {finalScrollX:F1}.  Indicates the " +
                "_pendingScrollX/Y guard is not working (intermediate ScrollChanged(0) " +
                "overwrote the intended offset).");

            Assert.True(stX > 100,
                $"ScrollTarget.X should mirror the final scroll; got stX={stX:F1}.");

            // ── Assert B: pan right must not be locked.
            const float panAmt = 150f;
            ctrl.SimulatePanStart(400f, 300f);
            ctrl.SimulatePanMove(400f + panAmt, 300f);
            ctrl.SimulatePanEnd();
            Dispatcher.UIThread.RunJobs();

            double scrollAfterPan = sv.Offset.X;
            Assert.True(Math.Abs((finalScrollX - scrollAfterPan) - panAmt) < 5.0,
                $"Pan right {panAmt}px should decrease scroll by {panAmt}px; " +
                $"before={finalScrollX:F1} after={scrollAfterPan:F1} " +
                $"delta={finalScrollX - scrollAfterPan:F1}.  Pan is locked.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression test matching the exact user repro:
    ///   add animation → add frame → load sprite → zoom to 100% → ONE mouse-wheel
    ///   notch up → attempt to pan right → LOCKED.
    ///
    /// This tests the overflow-entry scenario with a SINGLE zoom step across the
    /// free-pan ↔ scroll boundary.  The image is sized dynamically so it fits the
    /// headless viewport at 100% zoom but overflows after one 1.25× notch, regardless
    /// of the actual headless window size.
    ///
    /// The fix: <c>MeasureOverride</c> queues <c>Post(ApplyPendingScrollIfNeeded)</c>
    /// only after the content has been re-measured at the new zoom, ensuring the
    /// scroll offset is applied on correctly-sized content and is not clamped to 0.
    /// </summary>
    [AvaloniaFact]
    public void ZoomOverflow_OneWheelNotchFromFit_PanRightNotLocked()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            // Load a small placeholder first so the ScrollViewer has a real viewport.
            var seedPath = WriteSolidPng(dir, "seed.png", size: 32);
            ctrl.LoadTexture(seedPath);
            Dispatcher.UIThread.RunJobs();

            double vpW = sv.Viewport.Width;
            double vpH = sv.Viewport.Height;

            // Build an image that:
            //   - fits at 100% zoom:   imgSize < vpW
            //   - overflows at 125% :  imgSize * 1.25 > vpW  (one wheel notch of ×1.25)
            // Choosing 90% of vpW satisfies both: 0.9*vpW < vpW AND 0.9*1.25*vpW = 1.125*vpW > vpW.
            int imgSize = vpW > 50 ? (int)(vpW * 0.9) : 700;
            Assert.True((double)imgSize < vpW && imgSize * 1.25 > vpW,
                $"Test precondition failed: imgSize={imgSize} must fit at 100% and overflow at 125%; vpW={vpW:F1}");

            var texPath = WriteSolidPng(dir, "onenotch.png", imgSize, imgSize);
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // CenterFit sets zoom to ~0.85× (85% scale to leave margins), not 1.0.
            // Explicitly go to 100% so we start from a known, deterministic state
            // where one 1.25× wheel notch will cross the overflow boundary.
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            // Confirm scroll is in a valid centred state before zooming (always-scroll: offset ≥ 0).
            Assert.True(sv.Offset.X >= 0,
                $"Before zoom, scroll.X should be ≥ 0 (centred scroll active); got {sv.Offset.X:F1}");

            // One wheel notch toward the horizontal centre of the viewport.
            float pivotX = (float)(vpW / 2);
            float pivotY = (float)(vpH / 2);
            ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);

            // Confirm the pending scroll is set before RunJobs (diagnostic: helps pinpoint the failure).
            Assert.True(ctrl.PendingScrollApply,
                "After wheel zoom, _pendingScrollApply should be true. " +
                "If false, ZoomToward may not have detected overflow or QueueScrollAfterLayout was not called.");
            Assert.True(ctrl.PendingScrollTarget.X > 0,
                $"After wheel zoom, pending scroll X should be > 0; got {ctrl.PendingScrollTarget.X:F1}. " +
                "If 0, the overflow math may have computed newScrollX=0.");

            Dispatcher.UIThread.RunJobs();  // layout + deferred Post

            double scroll0 = sv.Offset.X;
            Assert.True(scroll0 > 0,
                $"After one wheel-zoom notch into overflow, scroll should be > 0; got {scroll0:F1}. " +
                "MeasureOverride Post did not apply the pending scroll.");

            // Pan right 100 px: scroll should decrease by 100 (not stay at 0 = locked).
            const float panPx = 100f;
            ctrl.SimulatePanStart(pivotX, pivotY);
            ctrl.SimulatePanMove(pivotX + panPx, pivotY);
            ctrl.SimulatePanEnd();

            double scroll1 = sv.Offset.X;
            Assert.True(Math.Abs((scroll0 - scroll1) - panPx) < 5.0,
                $"Pan right {panPx}px should decrease scroll by {panPx}; " +
                $"before={scroll0:F1} after={scroll1:F1} delta={scroll0 - scroll1:F1}.  Pan locked.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression test for <c>StartPan</c> clearing <c>_pendingScrollApply</c> so the
    /// deferred <c>Post(ApplyPendingScrollIfNeeded)</c> does not fire and override the
    /// user's pan gesture in progress.
    ///
    /// Sequence:
    /// 1. Zoom to 250% WITHOUT RunJobs: <c>_pendingScrollApply = true</c>,
    ///    <c>_pendingScrollX = X &gt; 0</c>.
    /// 2. <c>SimulatePanStart</c> BEFORE RunJobs — fix clears <c>_pendingScrollApply</c>
    ///    and records <c>_pendingScrollX</c> as the anchor.
    /// 3. <c>RunJobs</c> — layout at 250%.  Because the flag was cleared, MeasureOverride
    ///    does NOT queue a new Post; no scroll override happens.
    /// 4. <c>SimulatePanMove(+100)</c> — scroll = pendingScrollX − 100 &gt; 0.
    ///
    /// Without the fix, the Post would fire during RunJobs and override the pan anchor,
    /// or StartPan would use <c>_scrollTargetX</c> which could be corrupted to 0 in the
    /// live app by a spurious ScrollChanged between ZoomToward and layout.
    /// </summary>
    [AvaloniaFact]
    public void ZoomOverflow_StartPanWhilePendingApply_UsesCorrectScrollAnchor()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "anchor512.png", size: 512);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Establish a clean 100% baseline (no overflow, sv.Offset.X = 0).
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            Assert.True(sv.Offset.X >= 0,
                $"Baseline: at 100% zoom sv.Offset.X should be ≥ 0 (centred scroll); got {sv.Offset.X:F1}");

            float vpCx = (float)(sv.Viewport.Width  / 2);
            float vpCy = (float)(sv.Viewport.Height / 2);

            // ── Zoom to 250% WITHOUT RunJobs. ──
            // QueueScrollAfterLayout sets _pendingScrollX = _scrollTargetX = X > 0.
            ctrl.SetZoomPercent(250);

            double pendingX = ctrl.PendingScrollTarget.X;
            Assert.True(ctrl.PendingScrollApply,
                "After SetZoomPercent(250) without RunJobs, _pendingScrollApply should be true.");
            Assert.True(pendingX > 0,
                $"_pendingScrollX should be > 0 after zooming to 250%; got {pendingX:F1}.");

            // ── Pan starts BEFORE RunJobs (flag still true). ──
            // Fix: clears _pendingScrollApply so the Post queued by MeasureOverride won't
            //      fire and override the pan.  Also reads _pendingScrollX as anchor.
            ctrl.SimulatePanStart(vpCx, vpCy);

            Assert.False(ctrl.PendingScrollApply,
                "StartPan should clear _pendingScrollApply so no deferred Post overrides the pan.");

            // ── Run layout (250% content size, no Post because flag was cleared). ──
            Dispatcher.UIThread.RunJobs();

            // ── Pan right 100 px — scroll should move from anchor (pendingX). ──
            const float panPx = 100f;
            ctrl.SimulatePanMove(vpCx + panPx, vpCy);
            ctrl.SimulatePanEnd();

            double scrollAfterPan = sv.Offset.X;

            // If the deferred Post had fired (fix absent), it would override sv.Offset
            // back to pendingX before the move landed, and the result would be pendingX
            // (not pendingX − 100).  If anchor was wrong (0), result would be ≤ 0.
            Assert.True(scrollAfterPan > 0,
                $"Pan right {panPx}px from anchor≈{pendingX:F1} should yield scroll > 0; " +
                $"got {scrollAfterPan:F1}.");

            Assert.True(Math.Abs((pendingX - scrollAfterPan) - panPx) < 10.0,
                $"Scroll should be ≈ pendingX({pendingX:F1}) − panPx({panPx}) = {pendingX - panPx:F1}; " +
                $"got {scrollAfterPan:F1}.  Pan anchor or deferred Post interference suspected.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression: zooming toward the far-right edge of the image at moderate zoom can
    /// produce a <c>_pendingScrollX</c> that exceeds the new <c>maxScrollX</c> — a
    /// "legitimate overshoot" that must be clamped, NOT deferred indefinitely.
    ///
    /// Old extent guard: <c>_pendingScrollX &gt; maxScrollX + 2 → defer</c>
    ///   → would loop forever because the extent was already correct.
    ///
    /// Fixed guard: compare <c>sv.Extent.Width</c> against the MINIMUM required
    ///   (<c>_pendingScrollX + Viewport.Width</c>).  If extent is adequate, clamp and
    ///   apply immediately.  This test verifies:
    ///   1. The scroll is applied (not stuck at 0 or deferred).
    ///   2. The applied value is clamped to <c>maxScrollX</c>.
    ///   3. Pan right is NOT locked afterward.
    /// </summary>
    [AvaloniaFact]
    public void ZoomTowardRightEdge_OvershootTarget_ClampedAndPanNotLocked()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            // Use a seed image first so the ScrollViewer has a real viewport.
            var seedPath = WriteSolidPng(dir, "seed.png", size: 32);
            ctrl.LoadTexture(seedPath);
            Dispatcher.UIThread.RunJobs();

            double vpW = sv.Viewport.Width;
            double vpH = sv.Viewport.Height;

            // Image that overflows at 125% (same sizing formula as other tests).
            int imgSize = vpW > 50 ? (int)(vpW * 0.9) : 700;
            var texPath = WriteSolidPng(dir, "rightedge.png", imgSize, imgSize);
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Start at 100% (no overflow, offset = 0).
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();
            Assert.True(sv.Offset.X >= 0,
                $"Baseline: at 100% scroll should be ≥ 0 (centred scroll); got {sv.Offset.X:F1}");

            // Zoom toward the RIGHT EDGE of the viewport (pivotX = vpW * 0.95).
            // A pivot that far to the right produces newScrollX that can exceed
            // newMaxScrollX — the "legitimate overshoot" scenario.
            float pivotX = (float)(vpW * 0.95);
            float pivotY = (float)(vpH / 2);
            ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);
            Dispatcher.UIThread.RunJobs();

            // The extent is now correct (MeasureOverride committed the new content size).
            double extentW  = sv.Extent.Width;
            double maxScroll = Math.Max(0, extentW - vpW);

            // 1. Scroll must have been applied (not stuck at 0).
            double scroll0 = sv.Offset.X;
            Assert.True(scroll0 > 0,
                $"After zooming toward right edge, scroll should be > 0; got {scroll0:F1}. " +
                $"Pending scroll may have been deferred forever by old extent guard.");

            // 2. Scroll must be ≤ maxScrollX (overshoot was clamped, not deferred forever).
            Assert.True(scroll0 <= maxScroll + 2.0,
                $"Scroll {scroll0:F1} should be ≤ maxScroll {maxScroll:F1}. " +
                $"Overshoot target was not clamped.");

            // 3. Pan right must NOT be locked.
            const float panPx = 80f;
            ctrl.SimulatePanStart(pivotX, pivotY);
            ctrl.SimulatePanMove(pivotX + panPx, pivotY);
            ctrl.SimulatePanEnd();

            double scroll1 = sv.Offset.X;
            // Panning right from near-max-scroll may clamp at 0; allow it.
            // The key assertion is that scroll changed OR was already at the min boundary.
            Assert.True(scroll1 < scroll0 || scroll0 < 5.0,
                $"Pan right {panPx}px should move scroll left (or scroll was already near 0); " +
                $"before={scroll0:F1} after={scroll1:F1}. Pan locked.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression: when the image overflows only in Y (not X) — e.g., a narrow image on
    /// a wide monitor — horizontal panning must not be locked.
    ///
    /// In always-scroll mode both axes always use the scroll offset, so panning right
    /// decreases <c>sv.Offset.X</c> rather than adjusting <c>_panX</c> directly.
    /// The content is always wider than the viewport by at least
    /// <c>2 × PanPadding</c> pixels, so there is always scroll room in X.
    ///
    /// Repro scenario (from user): 512 px-wide image on a 1436 px-wide viewport.
    /// At moderate zoom (e.g. 150 %), the image overflows in Y but not X.  Dragging
    /// horizontally was completely locked before the per-axis fix.
    /// </summary>
    [AvaloniaFact]
    public void HybridOverflow_YOnlyOverflows_PanXUsesScrollPan()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            // Load a seed image so the ScrollViewer has a real (non-zero) viewport.
            var seedPath = WriteSolidPng(dir, "seed.png", size: 32);
            ctrl.LoadTexture(seedPath);
            Dispatcher.UIThread.RunJobs();

            double vpW = sv.Viewport.Width;
            double vpH = sv.Viewport.Height;

            // Create a NARROW, TALL image:
            //   imgW = 30% of vpW  → at 125% zoom: 0.3 * 1.25 * vpW = 0.375 * vpW  < vpW (X never overflows)
            //   imgH = 85% of vpH  → at 125% zoom: 0.85 * 1.25 * vpH = 1.0625 * vpH > vpH (Y overflows)
            int imgW = vpW > 50 ? (int)(vpW * 0.30) : 200;
            int imgH = vpH > 50 ? (int)(vpH * 0.85) : 400;

            // Verify test preconditions.
            Assert.True(imgW * 1.25 < vpW,
                $"Test precondition: imgW({imgW}) * 1.25 = {imgW * 1.25:F0} must be < vpW({vpW:F0})");
            Assert.True(imgH * 1.25 > vpH,
                $"Test precondition: imgH({imgH}) * 1.25 = {imgH * 1.25:F0} must be > vpH({vpH:F0})");

            var texPath = WriteSolidPng(dir, "narrow_tall.png", imgW, imgH);
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Set 100% zoom so both axes fit.
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            Assert.True(sv.Offset.Y >= 0,
                $"At 100% zoom, scroll.Y should be ≥ 0 (centred scroll); got {sv.Offset.Y:F1}");

            // One wheel notch (×1.25) → Y overflows, X does not.
            float pivotX = (float)(vpW / 2);
            float pivotY = (float)(vpH / 2);
            ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);
            Dispatcher.UIThread.RunJobs();

            // Verify post-zoom state: Y must overflow in image-vs-viewport terms.
            Assert.True(sv.Extent.Height > sv.Viewport.Height,
                $"After 1.25× zoom, image should overflow in Y; " +
                $"extent.H={sv.Extent.Height:F0} vp.H={sv.Viewport.Height:F0}");

            // In always-scroll mode both axes use the scroll offset regardless of whether
            // the image overflows the viewport in that axis.  Record the pre-pan offset.
            double scrollXBefore = sv.Offset.X;

            // Pan right 80 px: sv.Offset.X should decrease (panning right = image moves right
            // = less content to the left = lower scroll offset).
            const float panPx = 80f;
            ctrl.SimulatePanStart(pivotX, pivotY);
            ctrl.SimulatePanMove(pivotX + panPx, pivotY);
            ctrl.SimulatePanEnd();

            double scrollXAfter = sv.Offset.X;

            // Allow for clamping at 0 if the centred scroll was already near the minimum.
            Assert.True(scrollXAfter < scrollXBefore || scrollXBefore < 5.0,
                $"Pan right {panPx}px should decrease sv.Offset.X (or it was already at min); " +
                $"before={scrollXBefore:F1} after={scrollXAfter:F1}. X pan locked.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Camera-by-texture restore: PanX must not be 0 after switching back ────

    /// <summary>
    /// Regression guard for the camera-restore bug: when the user navigates
    /// away from texture A (causing it to be saved in _cameraByTexture) and
    /// then returns to it, PanX must equal EffectivePaddingX — NOT 0.
    ///
    /// If PanX is 0, the image is drawn at content-space origin, and all the
    /// dynamic padding (EffectivePaddingX) is placed to the RIGHT of the image
    /// instead of to the left.  The user cannot scroll left to see the space
    /// before the image's left edge.
    /// </summary>
    [AvaloniaFact]
    public void SwitchTextureAndBack_PanXEqualsEffectivePadding_NotZero()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texA = WriteSolidPng(dir, "a.png", size: 32);
            var texB = WriteSolidPng(dir, "b.png", size: 64);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            // Load texture A — fresh load, CenterTexture runs.
            ctrl.LoadTexture(texA);
            Dispatcher.UIThread.RunJobs();

            var (panXA1, _, _) = ctrl.CameraState;
            Assert.True(panXA1 > 0,
                $"Precondition: PanX after first load of A must be > 0; got {panXA1:F1}");

            // Navigate to B — this saves A's camera in _cameraByTexture.
            ctrl.LoadTexture(texB);
            Dispatcher.UIThread.RunJobs();

            // Navigate back to A — should restore via _cameraByTexture.
            // The critical invariant: PanX must still be > 0 (= EffectivePaddingX).
            // If PanX == 0 the user cannot scroll left to see the area before the image.
            ctrl.LoadTexture(texA);
            Dispatcher.UIThread.RunJobs();

            var (panXA2, panYA2, _) = ctrl.CameraState;
            Assert.True(panXA2 > 0,
                $"After switching back to A via _cameraByTexture restore, " +
                $"PanX must be > 0 (EffectivePaddingX); got {panXA2:F1}. " +
                $"A PanX=0 means the image is at content origin with no left-scroll space.");
            Assert.True(panYA2 > 0,
                $"PanY must also be > 0 after restore; got {panYA2:F1}");

            // The scrollbar should be active: sv.Extent.Width > sv.Viewport.Width.
            Assert.True(sv.Extent.Width > sv.Viewport.Width,
                $"After restore, scrollbar should be active " +
                $"(extent={sv.Extent.Width:F0} > vp={sv.Viewport.Width:F0})");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Zoom-to-cursor: scroll-bounds drift ───────────────────────────────────

    /// <summary>
    /// Regression guard for the zoom-to-cursor scroll-bounds drift bug (#138).
    ///
    /// Root cause: <c>ZoomToward</c> assigns <c>_panX = EffectivePaddingX()</c>
    /// unconditionally when scroll mode is active, but <c>TryApplyPendingScroll</c>
    /// later clamps the applied scroll to <c>maxScrollX</c>.  The un-compensated
    /// mismatch causes the content-space coordinate under the cursor to drift after
    /// every zoom step when the scroll is pinned at its maximum.
    ///
    /// Repro: 100×100 image at 100 % zoom, panned to <c>maxScrollX</c>, cursor placed
    /// 10 px past the right edge of the image (content coord 110).  A 2× zoom-in
    /// computes <c>newScrollX &gt; maxScrollX</c>.
    ///
    /// In the fixed-padding regime (narrow headless viewport ≈ 432 px wide):
    ///   <c>overflow = deltaZoom × (wx − imgW) = 1 × 10 = 10</c>, drift = 5 px.
    /// In the dynamic-padding regime (wider viewport):
    ///   overflow is even larger (≥ 60 px), drift ≥ 30 px.
    ///
    /// Without the fix, <c>_panX</c> stays at <c>epX</c> while <c>sv.Offset.X</c>
    /// is clamped, causing a drift of at least 5 px — well above the 1-px threshold.
    ///
    /// Fix: pre-clamp <c>newScrollX</c> inside <c>ZoomToward</c> and subtract the
    /// overflow from <c>_panX</c> so the cursor-to-content mapping is preserved.
    /// </summary>
    [AvaloniaFact]
    public void ZoomIn_WhenScrollAtRightBound_CursorContentCoordIsPreserved()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            // 100×100 image.  At zoom=1 the cursor 10 px past the right edge (content
            // coord 110) guarantees overflow = deltaZoom*(wx−imgW) in BOTH the fixed-
            // padding regime (typical narrow headless viewport) and the dynamic regime.
            const int   imgSize = 100;
            const float tx0     = imgSize + 10f;  // content coord to preserve (= 110)
            var texPath = WriteSolidPng(dir, "drift.png", imgSize, imgSize);
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            double vpW = sv.Viewport.Width;
            double vpH = sv.Viewport.Height;

            // Zoom to 100 % so the calculation below has a known zoom0 = 1.0.
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            // Scroll to the right boundary by panning far enough left.
            // maxScrollX ≤ 300 for any reasonable headless viewport, so 400 px is safe.
            ctrl.SimulatePanStart((float)(vpW / 2), (float)(vpH / 2));
            ctrl.SimulatePanMove((float)(vpW / 2) - 400f, (float)(vpH / 2));
            ctrl.SimulatePanEnd();
            Dispatcher.UIThread.RunJobs();

            // ── Record pre-zoom state ──────────────────────────────────────────
            float zoom0    = ctrl.CameraState.Zoom;   // 1.0
            float panX0    = ctrl.PanOffset.X;
            float scrollX0 = (float)sv.Offset.X;     // = maxScrollX after pan

            // Derive vpX so the content coord under the cursor equals tx0 exactly.
            float vpX = panX0 + tx0 * zoom0 - scrollX0;
            Assert.True(vpX >= 0 && vpX <= (float)vpW,
                $"Setup: vpX={vpX:F1} must be in [0, vpW={vpW:F1}].  " +
                $"panX0={panX0:F1}  scrollX0={scrollX0:F1}  zoom0={zoom0:F3}");

            // ── One 2× zoom-in at the cursor position ──────────────────────────
            ctrl.SimulateWheelZoom(vpX, (float)(vpH / 2), 2.0f);
            Dispatcher.UIThread.RunJobs();

            // Invariant: zoom-toward-boundary must not break pan-centering (#341).
            AssertZoomInvariants(ctrl, sv, "after 2× zoom at right scroll boundary");

            // ── Assert: content coord under cursor must be preserved ───────────
            // sv.Offset.X is the scroll actually applied (clamped to maxScrollX).
            // With the fix, _panX absorbs the overflow so the mapping is exact.
            // Without the fix, drift ≥ 5 px (fixed regime) or ≥ 30 px (dynamic).
            float scrollX1 = (float)sv.Offset.X;
            float panX1    = ctrl.PanOffset.X;
            float zoom1    = ctrl.CameraState.Zoom;

            float txAfter = (vpX + scrollX1 - panX1) / zoom1;

            Assert.True(Math.Abs(txAfter - tx0) < 1.0f,
                $"Content coord under cursor must be preserved after 2× zoom at scroll boundary; " +
                $"expected≈{tx0:F3}  got={txAfter:F3}  drift={txAfter - tx0:F3}. " +
                $"scroll: {scrollX0:F1}→{scrollX1:F1}  panX: {panX0:F1}→{panX1:F1}  " +
                $"zoom: {zoom0:F3}→{zoom1:F3}  vpX={vpX:F1}  vpW={vpW:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Zoom toward blank space: sprite must not go off-screen ────────────────

    /// <summary>
    /// Regression guard for #319: zooming repeatedly toward blank space far from
    /// a small image must not push the sprite outside the scrollable area, where
    /// it becomes permanently unreachable.
    ///
    /// Root cause: each zoom step computes <c>_panX = epX − (rawScrollX − maxScrollX)</c>.
    /// When the pivot is far from the image, the overshoot <c>(rawScrollX − maxScrollX)</c>
    /// grows faster than <c>epX</c>, so <c>_panX</c> goes negative.  Since scroll
    /// cannot go below 0, a negative <c>_panX</c> places the image to the left of
    /// scroll-origin-0, making it invisible and unreachable by any scroll or pan gesture.
    ///
    /// Fix: <c>_panX</c> is clamped to ≥ 0 and the scroll target is adjusted so the
    /// image right edge remains visible at the post-zoom viewport position.
    /// </summary>
    [AvaloniaFact]
    public void ZoomTowardBlankSpace_Repeatedly_DoesNotPushSpriteOffScreen()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "small.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // 100% zoom for a deterministic starting state.
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            double vpW = sv.Viewport.Width;
            double vpH = sv.Viewport.Height;

            // Zoom 8 notches toward the far corner — well past the 32×32 image.
            float pivotX = (float)(vpW * 0.90);
            float pivotY = (float)(vpH * 0.90);
            for (int i = 0; i < 8; i++)
                ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);
            Dispatcher.UIThread.RunJobs();

            // panX/Y ≥ 0 AND sprite centre reachable by scrolling (#319, #341).
            AssertZoomInvariants(ctrl, sv, "after 8 blank-space zoom steps");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression guard for the zoom-to-cursor scroll-bounds drift bug (#138) with
    /// three rapid successive wheel-zoom notches.
    ///
    /// Each 1.25× notch queued back-to-back (no <c>RunJobs</c> between them) chains off
    /// <c>_scrollTargetX</c> and <c>_panX</c> from the previous step.  Without the fix
    /// the drift accumulates; with the fix each step preserves the content-space anchor
    /// and the invariant holds after <c>RunJobs</c> flushes all pending applies.
    ///
    /// Uses the same 100×100 / cursor-past-right-edge setup as the single-zoom test so
    /// overflow is guaranteed in both the fixed- and dynamic-padding regimes.
    /// </summary>
    [AvaloniaFact]
    public void ZoomIn_RapidSuccessiveZooms_NoDriftAtScrollBound()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            const int   imgSize = 100;
            const float tx0     = imgSize + 10f;  // content coord to preserve (= 110)
            var texPath = WriteSolidPng(dir, "drift2.png", imgSize, imgSize);
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            double vpW = sv.Viewport.Width;
            double vpH = sv.Viewport.Height;

            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            // Scroll to the right boundary.
            ctrl.SimulatePanStart((float)(vpW / 2), (float)(vpH / 2));
            ctrl.SimulatePanMove((float)(vpW / 2) - 400f, (float)(vpH / 2));
            ctrl.SimulatePanEnd();
            Dispatcher.UIThread.RunJobs();

            float zoom0    = ctrl.CameraState.Zoom;
            float panX0    = ctrl.PanOffset.X;
            float scrollX0 = (float)sv.Offset.X;

            float vpX = panX0 + tx0 * zoom0 - scrollX0;
            Assert.True(vpX >= 0 && vpX <= (float)vpW,
                $"Setup: vpX={vpX:F1} must be in [0, vpW={vpW:F1}].  " +
                $"panX0={panX0:F1}  scrollX0={scrollX0:F1}  zoom0={zoom0:F3}");

            // Three rapid zoom-in notches with no RunJobs between them.
            ctrl.SimulateWheelZoom(vpX, (float)(vpH / 2), 1.25f);
            ctrl.SimulateWheelZoom(vpX, (float)(vpH / 2), 1.25f);
            ctrl.SimulateWheelZoom(vpX, (float)(vpH / 2), 1.25f);
            Dispatcher.UIThread.RunJobs();

            // Invariant: rapid zoom at boundary must preserve pan-centering (#341).
            AssertZoomInvariants(ctrl, sv, "after 3 rapid 1.25× zooms at right scroll boundary");

            float scrollX1 = (float)sv.Offset.X;
            float panX1    = ctrl.PanOffset.X;
            float zoom1    = ctrl.CameraState.Zoom;

            float txAfter = (vpX + scrollX1 - panX1) / zoom1;

            Assert.True(Math.Abs(txAfter - tx0) < 1.5f,
                $"Content coord under cursor must be preserved after 3 rapid 1.25× zooms at scroll boundary; " +
                $"expected≈{tx0:F3}  got={txAfter:F3}  drift={txAfter - tx0:F3}. " +
                $"scroll: {scrollX0:F1}→{scrollX1:F1}  panX: {panX0:F1}→{panX1:F1}  " +
                $"zoom: {zoom0:F3}→{zoom1:F3}  vpX={vpX:F1}  vpW={vpW:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Bottom-right zoom pan-centering bug (#341) ────────────────────────────

    /// <summary>
    /// Regression guard for the "sprite stuck near top-left after zooming from
    /// bottom-right corner" pan-centering bug (#341).
    ///
    /// Root cause: after multiple zoom-in steps with the pivot at the bottom-right
    /// corner, <c>ZoomToward</c> computes a very negative <c>rawPanX</c>.  The #319
    /// guard clamped <c>_panX = 0</c>, erasing the effective-padding buffer.  With
    /// <c>_panX = 0</c> and a small image the sprite's centre maps to a negative
    /// scroll offset — unreachable because scroll ≥ 0 — so the user cannot pan the
    /// sprite to the viewport centre.
    ///
    /// Fix: clamp to <c>epX</c> (effective-padding X) instead of 0, preserving the
    /// left-side buffer so the sprite is always pannable to the viewport centre.
    /// </summary>
    [AvaloniaFact]
    public void ZoomFromBottomRight_MultipleTimes_SpritePannableToCenter()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 32×32 sprite — small enough that rawPanX becomes negative after ~5
            // bottom-right zoom steps, reliably triggering the clamped branch.
            var texPath = WriteSolidPng(dir, "small32.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Start from 100 % zoom for a deterministic initial state.
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            float vpW = (float)sv.Viewport.Width;
            float vpH = (float)sv.Viewport.Height;

            // Simulate 8 wheel-zoom-in notches from the bottom-right corner.
            // rawPanX goes negative after ~5 steps, triggering the _panX clamp.
            float pivotX = vpW - 10f;
            float pivotY = vpH - 10f;
            for (int i = 0; i < 8; i++)
                ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);

            Dispatcher.UIThread.RunJobs();

            // panX/Y ≥ 0 AND sprite centre reachable: covers #319 (sprite off-screen)
            // and #341 (centreScroll < 0, centering blocked).
            AssertZoomInvariants(ctrl, sv, "after 8 bottom-right zoom steps");

            // ── Assert: executing the rightward pan actually decreases scroll ──
            double scrollBefore = sv.Offset.X;
            const float panAmt = 50f;
            if (scrollBefore >= panAmt)
            {
                ctrl.SimulatePanStart(vpW / 2f, vpH / 2f);
                ctrl.SimulatePanMove(vpW / 2f + panAmt, vpH / 2f);
                ctrl.SimulatePanEnd();
                Dispatcher.UIThread.RunJobs();

                double scrollAfter = sv.Offset.X;
                Assert.True(Math.Abs(scrollBefore - scrollAfter - panAmt) < 5.0,
                    $"Rightward pan of {panAmt}px must decrease scroll by ~{panAmt}px; " +
                    $"before={scrollBefore:F1} after={scrollAfter:F1} " +
                    $"delta={scrollBefore - scrollAfter:F1}. Sprite is stuck (#341).");
            }

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Parameterized blank-space zoom invariants ─────────────────────────────

    /// <summary>
    /// Parameterized regression guard: zooming 8 notches toward any blank-space
    /// viewport position must always satisfy the core ZoomToward post-conditions.
    ///
    /// Covers #319 (0.9 pivot), #341 (0.99 pivot), and additional fractions in
    /// a single harness — a future incomplete clamping fix would fail at least
    /// one of these cases even if it accidentally passes the others.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(0.5f,  0.5f,  "viewport centre")]
    [InlineData(0.9f,  0.9f,  "#319 pivot (90% corner)")]
    [InlineData(0.99f, 0.99f, "#341 pivot (99% far corner)")]
    [InlineData(0.5f,  0.99f, "bottom-edge centre")]
    [InlineData(0.99f, 0.5f,  "right-edge centre")]
    public void ZoomTowardBlankSpace_AnyPivot_InvariantsHold(float fracX, float fracY, string label)
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "small.png", size: 32);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            float vpW = (float)sv.Viewport.Width;
            float vpH = (float)sv.Viewport.Height;

            float pivotX = vpW * fracX;
            float pivotY = vpH * fracY;
            for (int i = 0; i < 8; i++)
                ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);
            Dispatcher.UIThread.RunJobs();

            AssertZoomInvariants(ctrl, sv, $"[{label}] after 8 blank-space zoom steps");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
