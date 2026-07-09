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
/// Tests for WireframeControl.FitChainToView — zoom-to-fits the union of a chain's frames
/// into the viewport, unconditionally (unlike the single-frame FitFrameIfLargerThanViewport,
/// which only fits when the frame doesn't already fit).
/// </summary>
public class WireframeFitChainToViewTests
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
    /// FitChainToView frames the union of every frame's bounds — not just the first or last —
    /// so a chain whose frames are scattered across the sheet is fully visible.
    /// </summary>
    [AvaloniaFact]
    public void FitChainToView_FramesUnionBounds()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 1000×1000 texture. Three frames scattered across the sheet:
            // union in pixels is [100,100]..[900,700].
            var texPath = WriteSolidPng(dir, "tex.png", 1000, 1000);
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave
            {
                LeftCoordinate = 0.1f, TopCoordinate = 0.1f,
                RightCoordinate = 0.3f, BottomCoordinate = 0.3f,
            });
            chain.Frames.Add(new AnimationFrameSave
            {
                LeftCoordinate = 0.7f, TopCoordinate = 0.5f,
                RightCoordinate = 0.9f, BottomCoordinate = 0.7f,
            });
            chain.Frames.Add(new AnimationFrameSave
            {
                LeftCoordinate = 0.4f, TopCoordinate = 0.2f,
                RightCoordinate = 0.6f, BottomCoordinate = 0.4f,
            });

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Start far from the expected fit, so a no-op would be unambiguous.
            ctrl.SetCamera(0f, 0f, 1f);

            bool zoomChangedFired = false;
            ctrl.ZoomChanged += _ => zoomChangedFired = true;

            ctrl.FitChainToView(chain);
            Dispatcher.UIThread.RunJobs();

            float vpW = (float)ctrl.Bounds.Width;
            float vpH = (float)ctrl.Bounds.Height;

            var (expPanX, expPanY, expZoom) = CanvasTransform.FitRect(
                100f, 100f, 900f, 700f, vpW, vpH, fitFraction: 0.85f, maxZoom: 4f);

            var (panX, panY, zoom) = ctrl.CameraState;
            Assert.Equal(expZoom, zoom, 3);
            Assert.Equal(expPanX, panX, 1);
            Assert.Equal(expPanY, panY, 1);
            Assert.True(zoomChangedFired, "FitChainToView changed the zoom, so ZoomChanged must fire");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>No-op when the chain has no frames — nothing to fit, camera is left untouched.</summary>
    [AvaloniaFact]
    public void FitChainToView_EmptyChain_NoOp()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "tex.png", 500, 500);
            var chain = new AnimationChainSave { Name = "Empty" };

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");
            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            ctrl.SetCamera(12f, 34f, 2f);

            ctrl.FitChainToView(chain);
            Dispatcher.UIThread.RunJobs();

            var (panX, panY, zoom) = ctrl.CameraState;
            Assert.Equal(12f, panX, 3);
            Assert.Equal(34f, panY, 3);
            Assert.Equal(2f, zoom, 3);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
