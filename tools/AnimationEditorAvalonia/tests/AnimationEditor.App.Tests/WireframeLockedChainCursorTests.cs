using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A locked chain (issue #1032) must not show any drag/resize affordance in the Wireframe panel
/// -- the drag itself is already guarded (see <see cref="WireframeLockedChainDragTests"/>), but
/// the hover cursor must not imply a locked target is draggable either.
/// </summary>
public class WireframeLockedChainCursorTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier             = 1f;
        return ctx;
    }

    private static string WriteSolidPng(string dir, int size = 64, string name = "sprite.png")
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.DarkGray);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static (WireframeControl ctrl, string dir) BuildCtrlWithSelectedFrame(TestServices ctx, bool locked)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir);

        // UV 0.125->0.875 on a 64x64 texture -> screen rect (8,8,56,56) at camera(0,0,1);
        // TopLeft handle centre = (8-5, 8-5) = (3,3), matching WireframeHandlesAndBorderTests.
        var frame = new AnimationFrameSave
        {
            TextureName      = png,
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.125f, TopCoordinate    = 0.125f,
            RightCoordinate  = 0.875f, BottomCoordinate = 0.875f,
            ShapesSave = new ShapesSave(),
        };
        var chain = new AnimationChainSave { Name = "Test", IsLocked = locked };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, dir);
    }

    [AvaloniaFact]
    public void GetHoverCursorTypeForTest_ChainLocked_ReturnsNull()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithSelectedFrame(ctx, locked: true);
        try
        {
            Assert.Null(ctrl.GetHoverCursorTypeForTest(3f, 3f));
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void GetHoverCursorTypeForTest_ChainUnlocked_ReturnsResizeCursor()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithSelectedFrame(ctx, locked: false);
        try
        {
            Assert.Equal(StandardCursorType.TopLeftCorner, ctrl.GetHoverCursorTypeForTest(3f, 3f));
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void IsShowingAddFrameCursor_SelectedChainLocked_False()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithSelectedFrame(ctx, locked: true);
        try
        {
            ctrl.RefreshCursorForCtrlChange(isCtrl: true);
            Assert.False(ctrl.IsShowingAddFrameCursor,
                "Ctrl+click would add into the locked selected chain -- no-op -- so the add-frame cursor must not show.");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void IsShowingAddFrameCursor_SelectedChainUnlocked_True()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithSelectedFrame(ctx, locked: false);
        try
        {
            ctrl.RefreshCursorForCtrlChange(isCtrl: true);
            Assert.True(ctrl.IsShowingAddFrameCursor);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
