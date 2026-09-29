using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #1241: while Ctrl is held, the wireframe outlines the region a Ctrl+click would create. The
/// outline must be the exact region the click produces in each placement mode.
/// </summary>
public class WireframeAddFrameGhostTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static (WireframeControl ctrl, string dir) BuildCtrl(TestServices ctx, int size = 128)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = System.IO.Path.Combine(dir, "sheet.png");
        using (var bm = new SKBitmap(size, size))
        {
            bm.Erase(SKColors.DarkGray);
            using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
            System.IO.File.WriteAllBytes(png, data.ToArray());
        }

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        return (ctrl, dir);
    }

    private static SKRect? CaptureCtrlClick(WireframeControl ctrl, Action click)
    {
        SKRect? created = null;
        void Handler(int l, int t, int r, int b) => created = new SKRect(l, t, r, b);
        ctrl.FrameCreatedFromRegion += Handler;
        click();
        ctrl.FrameCreatedFromRegion -= Handler;
        return created;
    }

    [AvaloniaFact]
    public void GetAddFrameGhostForScreenPoint_PlainMode_MatchesCtrlClickRegion()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            var ghost = ctrl.GetAddFrameGhostForScreenPoint(50f, 60f);
            var created = CaptureCtrlClick(ctrl, () => ctrl.SimulatePlainCtrlClick(50f, 60f));

            Assert.NotNull(ghost);
            Assert.Equal(created, ghost);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void GetAddFrameGhostForScreenPoint_GridMode_IsTheCellUnderThePointer()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.SetGrid(true, 32);

            var ghost = ctrl.GetAddFrameGhostForScreenPoint(50f, 70f);

            Assert.Equal(new SKRect(32, 64, 64, 96), ghost);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void GetAddFrameGhostForScreenPoint_MagicWandMode_MatchesCtrlClickRegion()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.IsMagicWandMode = true;

            var ghost = ctrl.GetAddFrameGhostForScreenPoint(50f, 60f);
            var created = CaptureCtrlClick(ctrl, () => ctrl.SimulateWandCtrlClick(50f, 60f));

            Assert.NotNull(ghost);
            Assert.Equal(created, ghost);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void AddFrameGhost_CtrlReleased_IsCleared()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            ctrl.GetAddFrameGhostForScreenPoint(50f, 60f);

            ctrl.RefreshCursorForCtrlChange(isCtrl: false);

            Assert.Null(ctrl.AddFrameGhost);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void AddFrameGhost_CtrlPressedWhilePointerNotOverWireframe_IsNull()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            // MainWindow refreshes on every Ctrl press, including Ctrl+S with the pointer in the tree.
            ctrl.RefreshCursorForCtrlChange(isCtrl: true);

            Assert.Null(ctrl.AddFrameGhost);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void AddFrameGhost_AddTargetChainLocked_IsNull()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrl(ctx);
        try
        {
            var chain = new AnimationChainSave { Name = "Locked", IsLocked = true };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;

            Assert.Null(ctrl.GetAddFrameGhostForScreenPoint(50f, 60f));
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
