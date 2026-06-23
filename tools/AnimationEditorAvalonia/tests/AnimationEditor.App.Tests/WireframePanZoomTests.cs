using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Integration tests for WireframeControl's pan/zoom camera (#422). The control draws under a
/// manual pan/zoom camera and clamps analytically (no ScrollViewer); two ScrollBars are driven
/// from the camera. These tests verify the end-to-end behaviour the pure CanvasTransform tests
/// guarantee in the small: a symmetric zoom in/out round-trips, the reachable bounds at a given
/// zoom are direction-independent (the one-notch lag), and the texture is never lost off-edge and
/// is always pannable to the viewport centre (#319/#341).
/// </summary>
public class WireframePanZoomTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TestServices ResetSingletons()
    {
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

    // WireframeControl.PanPadding — the dead-space band the analytic clamp uses.
    private const float PanPadding = 300f;

    /// <summary>
    /// Asserts the two invariants every zoom/pan operation must preserve, recomputed from the
    /// control's public camera state against the same analytic clamp the control uses:
    /// <list type="bullet">
    ///   <item>#319 — the current camera pan is inside the valid band (texture never lost off-edge).</item>
    ///   <item>#341 — the centred camera (texture centre at viewport centre) is inside the band,
    ///         so the texture is always pannable to the viewport centre.</item>
    /// </list>
    /// </summary>
    private static void AssertCameraReachable(WireframeControl ctrl, string context)
    {
        var (panX, panY, zoom) = ctrl.CameraState;
        var (bw, bh) = ctrl.BitmapSize;
        float vpW = (float)ctrl.Bounds.Width, vpH = (float)ctrl.Bounds.Height;

        var (clampX, clampY) = CanvasTransform.ClampWireframePan(panX, panY, vpW, vpH, bw, bh, zoom, PanPadding);
        Assert.True(Math.Abs(clampX - panX) < 0.5f && Math.Abs(clampY - panY) < 0.5f,
            $"{context}: camera pan ({panX:F1},{panY:F1}) is outside the valid band " +
            $"(clamp → {clampX:F1},{clampY:F1}) — texture pushed off-edge (#319).");

        float centeredX = vpW / 2f - bw * zoom / 2f;
        float centeredY = vpH / 2f - bh * zoom / 2f;
        var (ccX, ccY) = CanvasTransform.ClampWireframePan(centeredX, centeredY, vpW, vpH, bw, bh, zoom, PanPadding);
        Assert.True(Math.Abs(ccX - centeredX) < 0.5f && Math.Abs(ccY - centeredY) < 0.5f,
            $"{context}: texture centre not pannable to viewport centre (#341).");
    }

    // ── Centering on load ─────────────────────────────────────────────────────

    [AvaloniaFact]
    public void LoadTexture_SmallImage_CentersTexture()
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

            var (panX, panY, zoom) = ctrl.CameraState;
            // Texture centre lands at the viewport centre: panX + bw*zoom/2 ≈ vpW/2.
            float centreX = panX + 32 * zoom / 2f;
            float centreY = panY + 32 * zoom / 2f;
            Assert.Equal(ctrl.Bounds.Width  / 2f, centreX, 1);
            Assert.Equal(ctrl.Bounds.Height / 2f, centreY, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SetZoomPercent_SlightZoomIn_KeepsTextureCentered()
    {
        // Guards the old "image jumps to the top-left corner on first zoom" bug: a slight zoom-in
        // on a centred small image must keep it centred, not reset the pan.
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

            int currentPct = (int)(ctrl.CameraState.Zoom * 100f);
            ctrl.SetZoomPercent((int)(currentPct * 1.1f));
            Dispatcher.UIThread.RunJobs();

            var (panX, panY, zoom) = ctrl.CameraState;
            Assert.Equal(ctrl.Bounds.Width  / 2f, panX + 32 * zoom / 2f, 1);
            Assert.Equal(ctrl.Bounds.Height / 2f, panY + 32 * zoom / 2f, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── #422 round-trip + direction independence ──────────────────────────────

    [AvaloniaFact]
    public void WheelZoom_SymmetricInOut_RoundTripsCamera()
    {
        // #422 acceptance: zoom in N notches toward the viewport centre, then out N notches,
        // and the camera returns exactly to its starting state — no one-notch lag.
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "medium.png", size: 256);
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            var (panX0, panY0, zoom0) = ctrl.CameraState;
            float pivotX = (float)(ctrl.Bounds.Width  / 2);
            float pivotY = (float)(ctrl.Bounds.Height / 2);

            for (int i = 0; i < 4; i++) ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);
            for (int i = 0; i < 4; i++) ctrl.SimulateWheelZoom(pivotX, pivotY, 1f / 1.25f);
            Dispatcher.UIThread.RunJobs();

            var (panX1, panY1, zoom1) = ctrl.CameraState;
            Assert.Equal(zoom0, zoom1, 4);
            Assert.Equal(panX0, panX1, 1);
            Assert.Equal(panY0, panY1, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SetZoomPercent_SameZoomFromInOrOut_ScrollBarRangesIdentical()
    {
        // #422 core: the reachable scroll/pan bounds at a given zoom must be identical regardless
        // of whether that zoom was reached by zooming in or out (the bug was a one-notch lag).
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "medium.png", size: 256);
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Reach 200 % by zooming IN from 100 %.
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();
            ctrl.SetZoomPercent(200);
            Dispatcher.UIThread.RunJobs();
            var (hIn, vIn) = ctrl.GetScrollBarRanges();

            // Reach 200 % by zooming OUT from 400 %.
            ctrl.SetZoomPercent(400);
            Dispatcher.UIThread.RunJobs();
            ctrl.SetZoomPercent(200);
            Dispatcher.UIThread.RunJobs();
            var (hOut, vOut) = ctrl.GetScrollBarRanges();

            Assert.Equal(hIn.Minimum, hOut.Minimum, 2);
            Assert.Equal(hIn.Maximum, hOut.Maximum, 2);
            Assert.Equal(vIn.Minimum, vOut.Minimum, 2);
            Assert.Equal(vIn.Maximum, vOut.Maximum, 2);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── #319 / #341 — texture never lost, always pannable to centre ───────────

    [AvaloniaTheory]
    [InlineData(0.5f,  0.5f,  "viewport centre")]
    [InlineData(0.9f,  0.9f,  "#319 pivot (90% corner)")]
    [InlineData(0.99f, 0.99f, "#341 pivot (99% far corner)")]
    [InlineData(0.99f, 0.5f,  "right-edge centre")]
    public void WheelZoom_TowardAnyPivot_Repeatedly_TextureStaysReachable(float fracX, float fracY, string label)
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
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            float pivotX = (float)ctrl.Bounds.Width  * fracX;
            float pivotY = (float)ctrl.Bounds.Height * fracY;
            for (int i = 0; i < 8; i++) ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);
            Dispatcher.UIThread.RunJobs();

            AssertCameraReachable(ctrl, $"[{label}] after 8 zoom steps toward the pivot");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void ZoomIn_PivotPreserved_CursorTextureCoordStable()
    {
        // #138: while the pan stays in-band, the texture coordinate under the zoom pivot is
        // preserved exactly across a zoom step (no boundary drift).
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "medium.png", size: 256);
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();
            ctrl.SetZoomPercent(100);
            Dispatcher.UIThread.RunJobs();

            float pivotX = (float)(ctrl.Bounds.Width  * 0.55);
            float pivotY = (float)(ctrl.Bounds.Height * 0.55);
            var (panX0, panY0, zoom0) = ctrl.CameraState;
            float texX0 = (pivotX - panX0) / zoom0;
            float texY0 = (pivotY - panY0) / zoom0;

            ctrl.SimulateWheelZoom(pivotX, pivotY, 1.25f);
            Dispatcher.UIThread.RunJobs();

            var (panX1, panY1, zoom1) = ctrl.CameraState;
            Assert.Equal(texX0, (pivotX - panX1) / zoom1, 1);
            Assert.Equal(texY0, (pivotY - panY1) / zoom1, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Pan ───────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Pan_DragRight_MovesCameraWithCursor()
    {
        // Grab-and-drag panning: dragging the pointer right moves the texture right (panX grows).
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "medium.png", size: 256);
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();
            ctrl.SetZoomPercent(200); // overflow so there is room to pan
            Dispatcher.UIThread.RunJobs();

            float cx = (float)(ctrl.Bounds.Width / 2), cy = (float)(ctrl.Bounds.Height / 2);
            float panX0 = ctrl.CameraState.PanX;

            const float drag = 60f;
            ctrl.SimulatePanStart(cx, cy);
            ctrl.SimulatePanMove(cx + drag, cy);
            ctrl.SimulatePanEnd();

            Assert.Equal(panX0 + drag, ctrl.CameraState.PanX, 1);
            AssertCameraReachable(ctrl, "after pan right");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void WireframeHScroll_SetValue_MovesPanInverted()
    {
        // Dragging the horizontal scrollbar pans the wireframe, inverted (scroll value = -pan_c).
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "medium.png", size: 256);
            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl    = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            var hscroll = FindCtrl<ScrollBar>(window, "WireframeHScroll");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();
            ctrl.SetZoomPercent(200);
            Dispatcher.UIThread.RunJobs();

            Assert.True(hscroll.Maximum > hscroll.Minimum, "expected a non-degenerate scroll range");

            hscroll.Value = hscroll.Minimum; // drag the thumb fully left
            Dispatcher.UIThread.RunJobs();

            // pan_c = -scrollValue, so panX = -min + viewW/2.
            float expectedPanX = -(float)hscroll.Minimum + (float)ctrl.Bounds.Width / 2f;
            Assert.Equal(expectedPanX, ctrl.CameraState.PanX, 2);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
