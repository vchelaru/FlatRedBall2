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
/// Tests for WireframeControl.CenterOnFrame — zooms to fit the frame and scrolls
/// so the frame region is centred in the viewport.
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
    /// CenterOnFrame zooms to fit the frame's bounding box at 85 % of the viewport
    /// and scrolls so the frame centre lands at the viewport centre.
    /// </summary>
    [AvaloniaFact]
    public void CenterOnFrame_ZoomsToFitFrameAndCenters()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 500×500 texture; frame UV (0.6–0.8, 0.6–0.8) → 100×100 pixel region,
            // centre at pixel (350, 350).
            const float bmpW   = 500f;
            const float bmpH   = 500f;
            const float frameW = 100f;
            const float frameH = 100f;
            const float texCX  = 350f;
            const float texCY  = 350f;

            var texPath = WriteSolidPng(dir, "tex.png", (int)bmpW, (int)bmpH);
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

            // Capture any ZoomChanged notification.
            float? zoomChangedValue = null;
            ctrl.ZoomChanged += v => zoomChangedValue = v;

            ctrl.CenterOnFrame(frame);
            Dispatcher.UIThread.RunJobs();

            float vpW = (float)ctrl.Bounds.Width;
            float vpH = (float)ctrl.Bounds.Height;

            // Zoom should fit the frame at 85 % of the viewport.
            float expectedZoom = Math.Clamp(
                Math.Min(vpW / frameW, vpH / frameH) * 0.85f,
                CanvasTransform.MinZoom, CanvasTransform.MaxZoom);

            Assert.True(Math.Abs(ctrl.Zoom - expectedZoom) < 0.01f,
                $"Zoom should fit frame; expected≈{expectedZoom:F3} actual={ctrl.Zoom:F3}");

            // ZoomChanged event must have fired with the new percentage.
            Assert.NotNull(zoomChangedValue);
            Assert.True(Math.Abs(zoomChangedValue!.Value - expectedZoom * 100f) < 1f,
                $"ZoomChanged value should be {expectedZoom * 100f:F1}%; got {zoomChangedValue.Value:F1}%");

            // The frame centre should land at the viewport centre: screenX = panX + texCX*zoom.
            var (panX, panY, zoom) = ctrl.CameraState;
            Assert.Equal(vpW / 2f, panX + texCX * zoom, 1);
            Assert.Equal(vpH / 2f, panY + texCY * zoom, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// CenterOnFrame on a tiny frame near the texture corner zooms in to fit the frame and clamps
    /// the camera to the valid pan band (the frame lands as close to centre as the dead-space
    /// allows) without pushing the texture off-edge.
    /// </summary>
    [AvaloniaFact]
    public void CenterOnFrame_FrameNearFarEdge_ZoomsAndClampsCamera()
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

            ctrl.CenterOnFrame(frame);
            Dispatcher.UIThread.RunJobs();

            float vpW = (float)ctrl.Bounds.Width;
            float vpH = (float)ctrl.Bounds.Height;

            // Zoom should fit the 25×25 frame at 85 % of the viewport.
            float frameFitZoom = Math.Clamp(
                Math.Min(vpW / 25f, vpH / 25f) * 0.85f,
                CanvasTransform.MinZoom, CanvasTransform.MaxZoom);
            Assert.True(Math.Abs(ctrl.Zoom - frameFitZoom) < 0.01f,
                $"Zoom should fit 25×25 frame; expected≈{frameFitZoom:F3} actual={ctrl.Zoom:F3}");

            // The camera must stay inside the valid pan band — re-clamping is a no-op.
            var (panX, panY, zoom) = ctrl.CameraState;
            var (bw, bh) = ctrl.BitmapSize;
            var (cx, cy) = CanvasTransform.ClampWireframePan(panX, panY, vpW, vpH, bw, bh, zoom, 300f);
            Assert.Equal(panX, cx, 1);
            Assert.Equal(panY, cy, 1);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
