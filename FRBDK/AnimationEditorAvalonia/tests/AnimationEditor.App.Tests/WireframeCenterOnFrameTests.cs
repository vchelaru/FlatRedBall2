using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall.Content.AnimationChain;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for WireframeControl.CenterOnFrame — scrolls the wireframe panel
/// so the specified frame's region is centred in the viewport.
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
        ctx.AppState.UnitType                     = UnitType.Pixel;
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
    /// CenterOnFrame scrolls the wireframe so the frame's centre is in
    /// the middle of the viewport.  Uses a 500×500 texture at 300% zoom so
    /// the image overflows and the expected scroll value is positive and not
    /// clamped to zero for any realistic headless viewport size.
    /// </summary>
    [AvaloniaFact]
    public void CenterOnFrame_FrameAtKnownUV_ScrollsToFrameCenter()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 500×500 so the image overflows at 300% zoom
            var texPath = WriteSolidPng(dir, "tex.png", 500, 500);

            // Frame with UV centre at (0.7, 0.7) → texture-space pixel centre = (350, 350)
            float expectedTexCX = 350f;
            float expectedTexCY = 350f;

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
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Zoom to 300% so the 500px image (1500px rendered) overflows the viewport,
            // guaranteeing the expected scroll values are positive.
            ctrl.SetZoomPercent(300);
            Dispatcher.UIThread.RunJobs();

            var (panX, panY, zoom) = ctrl.CameraState;
            double vpW = sv.Viewport.Width;
            double vpH = sv.Viewport.Height;

            float expectedScrollX = Math.Max(0f, panX + expectedTexCX * zoom - (float)vpW / 2f);
            float expectedScrollY = Math.Max(0f, panY + expectedTexCY * zoom - (float)vpH / 2f);

            // Sanity: expected scroll must be positive for the test to be meaningful
            Assert.True(expectedScrollX > 0,
                $"Test precondition failed: expectedScrollX={expectedScrollX:F1} must be > 0 " +
                $"(panX={panX:F1} zoom={zoom:F2} vpW={vpW:F1})");

            ctrl.CenterOnFrame(frame);
            Dispatcher.UIThread.RunJobs();

            Assert.True(Math.Abs(sv.Offset.X - expectedScrollX) < 2.0,
                $"Scroll X should centre frame; expected≈{expectedScrollX:F1} actual={sv.Offset.X:F1}");
            Assert.True(Math.Abs(sv.Offset.Y - expectedScrollY) < 2.0,
                $"Scroll Y should centre frame; expected≈{expectedScrollY:F1} actual={sv.Offset.Y:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// CenterOnFrame called with a frame near the far edge of the texture
    /// must clamp to max scroll and not leave the pending-scroll flag stuck.
    /// </summary>
    [AvaloniaFact]
    public void CenterOnFrame_FrameNearFarEdge_ClampsToMaxScroll()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "tex.png", 500, 500);

            // Frame at the far corner [0.95, 0.95, 1.0, 1.0] → centre at (487.5, 487.5)
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
            var sv   = FindCtrl<ScrollViewer>(window, "WireframeScrollViewer");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            ctrl.SetZoomPercent(300);
            Dispatcher.UIThread.RunJobs();

            ctrl.CenterOnFrame(frame);
            Dispatcher.UIThread.RunJobs();

            // Pending flag must be cleared — scroll was either applied or clamped
            Assert.False(ctrl.PendingScrollApply,
                "PendingScrollApply must be false after CenterOnFrame + RunJobs");

            // Scroll must not exceed max
            double maxScrollX = Math.Max(0, sv.Extent.Width  - sv.Viewport.Width);
            double maxScrollY = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
            Assert.True(sv.Offset.X <= maxScrollX + 1.0,
                $"Scroll X must not exceed max; offset={sv.Offset.X:F1} max={maxScrollX:F1}");
            Assert.True(sv.Offset.Y <= maxScrollY + 1.0,
                $"Scroll Y must not exceed max; offset={sv.Offset.Y:F1} max={maxScrollY:F1}");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
