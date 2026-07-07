using AnimationEditor.App.Controls;
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
/// Tests for the #616 auto-fit: a newly added or retextured frame that is larger than the wireframe
/// viewport is zoomed to fit so its edges/handles aren't stranded off-screen. Fitting must NOT happen
/// for a plain re-selection of an existing frame, nor when the frame already fits.
/// </summary>
public class WireframeFitNewFrameTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;   // TextureName is treated as an absolute path
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name, int size)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.Blue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    // A chain whose single whole-texture frame references the given absolute texture path.
    private static (AnimationChainListSave acls, AnimationChainSave chain, AnimationFrameSave frame)
        WholeTextureChain(string texPath)
    {
        var acls  = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Chain0" };
        var frame = new AnimationFrameSave
        {
            TextureName      = texPath,
            LeftCoordinate   = 0f, TopCoordinate    = 0f,
            RightCoordinate  = 1f, BottomCoordinate = 1f,
            FrameLength      = 0.1f,
            ShapesSave       = new ShapesSave(),
        };
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);
        return (acls, chain, frame);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adding a frame that covers a large sheet while zoomed in zooms the wireframe out until the
    /// whole frame fits, and raises ZoomChanged so the zoom combo re-syncs (#616).
    /// </summary>
    [AvaloniaFact]
    public void AddFrame_WholeTextureFrameZoomedIn_FitsFrameIntoView()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const int texSize = 2048;
            var texPath = WriteSolidPng(dir, "sheet.png", texSize);
            var (acls, chain, existing) = WholeTextureChain(texPath);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ctx.ProjectManager.AnimationChainListSave = acls;

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");

            // Select the existing frame so the sheet loads, then force a zoom-in where the whole
            // 2048² sheet is far larger than the viewport (all edges off-screen).
            ctx.SelectedState.SelectedFrame = existing;
            Dispatcher.UIThread.RunJobs();
            ctrl.SetCamera(0f, 0f, 4f);

            bool zoomChangedFired = false;
            ctrl.ZoomChanged += _ => zoomChangedFired = true;

            // Add a frame (inherits the whole-texture region), which arms and triggers the fit.
            ctx.AppCommands.AddFrame(chain);
            Dispatcher.UIThread.RunJobs();

            float vpW  = (float)ctrl.Bounds.Width;
            float vpH  = (float)ctrl.Bounds.Height;
            float zoom = ctrl.Zoom;

            Assert.True(zoomChangedFired, "Fitting a too-large frame must raise ZoomChanged.");
            Assert.True(zoom < 4f, "Zoom should have decreased to fit the frame.");
            Assert.True(texSize * zoom <= vpW + 0.5f, "Frame width must fit within the viewport.");
            Assert.True(texSize * zoom <= vpH + 0.5f, "Frame height must fit within the viewport.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Re-selecting an existing frame (no add, no retexture) must never change the zoom, even when
    /// the frame is larger than the viewport — the auto-fit is scoped to new/retextured frames (#616).
    /// </summary>
    [AvaloniaFact]
    public void SelectingExistingFrame_ZoomedIn_LeavesZoomUnchanged()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir, "sheet.png", 2048);
            var (acls, _, existing) = WholeTextureChain(texPath);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ctx.ProjectManager.AnimationChainListSave = acls;

            var ctrl = FindCtrl<WireframeControl>(window, "WireframeCtrl");

            ctx.SelectedState.SelectedFrame = existing;
            Dispatcher.UIThread.RunJobs();
            ctrl.SetCamera(0f, 0f, 4f);

            bool zoomChangedFired = false;
            ctrl.ZoomChanged += _ => zoomChangedFired = true;

            // Clear then re-select the same frame — a plain selection change, not a new frame.
            ctx.SelectedState.SelectedFrame = null;
            ctx.SelectedState.SelectedFrame = existing;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(4f, ctrl.Zoom, 3);
            Assert.False(zoomChangedFired, "A plain selection must not raise ZoomChanged.");

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
