using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for WireframeControl.ForceReloadTexture — used for PNG hot-reload (issue #584). Reloading
/// a changed PNG must refresh the texture in place without resetting the user's current pan/zoom.
/// </summary>
public class WireframeForceReloadZoomTests
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

    private static void WriteSolidPng(string path, int width, int height, SKColor color)
    {
        using var bm = new SKBitmap(width, height);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// ForceReloadTexture (the PNG hot-reload path) must preserve whatever pan/zoom the user had
    /// set before the file changed on disk, rather than resetting to the default centred fit.
    /// </summary>
    [AvaloniaFact]
    public void ForceReloadTexture_PreservesCurrentPanAndZoom()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = Path.Combine(dir, "tex.png");
            WriteSolidPng(texPath, 500, 500, SKColors.Blue);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");

            ctrl.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Deliberate non-default camera, distinct from whatever CenterTexture would produce.
            ctrl.SetCamera(37f, 41f, 2.5f);

            // Overwrite the same path with different content, as a hot-reload would observe.
            WriteSolidPng(texPath, 500, 500, SKColors.Red);

            ctrl.ForceReloadTexture();
            Dispatcher.UIThread.RunJobs();

            var (panX, panY, zoom) = ctrl.CameraState;
            Assert.Equal(37f, panX, 2);
            Assert.Equal(41f, panY, 2);
            Assert.Equal(2.5f, zoom, 3);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
