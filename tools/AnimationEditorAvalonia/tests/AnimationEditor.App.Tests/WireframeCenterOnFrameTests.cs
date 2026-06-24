using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for WireframeControl.CenterOnFrame — scrolls so the frame region is
/// centred in the viewport at the current zoom, without changing the zoom.
/// </summary>
public class WireframeCenterOnFrameTests
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

    private static string WriteSolidPng(string dir, string name, int width, int height)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(width, height);
        bm.Erase(SKColors.Blue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// CenterOnFrame on a tiny frame near the texture corner preserves the current zoom and keeps
    /// the camera inside the valid pan band (does not push the texture off-edge).
    /// </summary>
    [AvaloniaFact]
    public void CenterOnFrame_FrameNearFarEdge_PreservesZoomAndClampsCamera()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "tex.png", 500, 500);

            // Frame at the far corner [0.95, 0.95, 1.0, 1.0] — 25×25 pixels.
            var frame = new AnimationFrameSave
            {
                LeftCoordinate   = 0.95f,
                TopCoordinate    = 0.95f,
                RightCoordinate  = 1.0f,
                BottomCoordinate = 1.0f,
            };

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Start at a deliberate 3× zoom; CenterOnFrame must leave it untouched.
            ctrl.SetCamera(0f, 0f, 3f);

            bool zoomChangedFired = false;
            ctrl.ZoomChanged += _ => zoomChangedFired = true;

            ctrl.CenterOnFrame(frame);
            Dispatcher.UIThread.RunJobs();

            float vpW = (float)ctrl.Bounds.Width;
            float vpH = (float)ctrl.Bounds.Height;

            // Zoom is preserved and no zoom notification fires.
            Assert.Equal(3f, ctrl.Zoom, 3);
            Assert.False(zoomChangedFired, "CenterOnFrame must not raise ZoomChanged");

            // The camera must stay inside the valid pan band — re-clamping is a no-op.
            var (panX, panY, zoom) = ctrl.CameraState;
            var (bw, bh) = ctrl.BitmapSize;
            var (cx, cy) = CanvasTransform.ClampWireframePan(panX, panY, vpW, vpH, bw, bh, zoom);
            Assert.Equal(panX, cx, 1);
            Assert.Equal(panY, cy, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// CenterOnFrame preserves the current zoom level and scrolls so the frame centre lands at the
    /// viewport centre.
    /// </summary>
    [AvaloniaFact]
    public void CenterOnFrame_PreservesZoomAndCentersFrame()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 500×500 texture; frame UV (0.6–0.8, 0.6–0.8) → 100×100 pixel region,
            // centre at pixel (350, 350).
            const float texCX = 350f;
            const float texCY = 350f;

            var texPath = WriteSolidPng(dir, "tex.png", 500, 500);
            var frame = new AnimationFrameSave
            {
                LeftCoordinate   = 0.6f,
                TopCoordinate    = 0.6f,
                RightCoordinate  = 0.8f,
                BottomCoordinate = 0.8f,
            };

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Start at a deliberate 1× zoom — far from the frame-fit zoom the old behaviour would
            // have jumped to — so a preserved zoom is unambiguous.
            ctrl.SetCamera(0f, 0f, 1f);

            bool zoomChangedFired = false;
            ctrl.ZoomChanged += _ => zoomChangedFired = true;

            ctrl.CenterOnFrame(frame);
            Dispatcher.UIThread.RunJobs();

            float vpW = (float)ctrl.Bounds.Width;
            float vpH = (float)ctrl.Bounds.Height;

            // Zoom is left exactly as it was; no zoom notification fires.
            Assert.Equal(1f, ctrl.Zoom, 3);
            Assert.False(zoomChangedFired, "CenterOnFrame must not raise ZoomChanged");

            // The frame centre lands at the viewport centre: screenX = panX + texCX*zoom.
            var (panX, panY, zoom) = ctrl.CameraState;
            Assert.Equal(vpW / 2f, panX + texCX * zoom, 1);
            Assert.Equal(vpH / 2f, panY + texCY * zoom, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
