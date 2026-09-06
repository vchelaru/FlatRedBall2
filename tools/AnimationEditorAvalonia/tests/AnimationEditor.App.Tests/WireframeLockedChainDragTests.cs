using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A locked chain (issue #1032) must be inert to Wireframe-panel drag gestures — resizing or
/// moving a frame's region on the sprite sheet must not work while its chain is locked. Mirrors
/// <see cref="PreviewLockedChainDragTests"/> for the top (texture-editor) panel.
/// </summary>
public class WireframeLockedChainDragTests
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

    private static string WriteSolidPng(string dir, SKColor color, int size = 64, string name = "sprite.png")
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static (WireframeControl ctrl, AnimationFrameSave frame, string dir) BuildCtrlWithSelectedFrame(
        TestServices ctx, bool locked)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

        var frame = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0f, TopCoordinate    = 0f,
            RightCoordinate  = 1f, BottomCoordinate = 1f,
            ShapesSave = new ShapesSave(),
        };
        var chain = new AnimationChainSave { Name = "Test", IsLocked = locked };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, frame, dir);
    }

    [AvaloniaFact]
    public void SimulateHandleDrag_ChainLocked_DoesNotChangeUVAndRecordsNoUndo()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx, locked: true);
        try
        {
            ctrl.SimulateHandleDrag(HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   8f);

            Assert.Equal(0f, frame.LeftCoordinate, precision: 4);
            Assert.Equal(0f, frame.TopCoordinate,  precision: 4);
            Assert.False(ctx.UndoManager.CanUndo);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SimulateChainDrag_ChainLocked_DoesNotChangeUVAndRecordsNoUndo()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx, locked: true);
        try
        {
            ctrl.SimulateChainDrag(
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   8f);

            Assert.Equal(0f, frame.LeftCoordinate, precision: 4);
            Assert.Equal(0f, frame.TopCoordinate,  precision: 4);
            Assert.False(ctx.UndoManager.CanUndo);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SimulateHandleDrag_ChainUnlocked_StillWorks()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx, locked: false);
        try
        {
            ctrl.SimulateHandleDrag(HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   8f);

            Assert.Equal(0.125f, frame.LeftCoordinate, precision: 4);
            Assert.True(ctx.UndoManager.CanUndo);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Bulk multi-chain resize: a locked chain among the visible frames keeps its own region
    /// still while the rest resize together, mirroring the bulk skip-locked-entries pattern.
    /// </summary>
    [AvaloniaFact]
    public void SimulateBulkHandleDrag_OneOfTwoChainsLocked_OnlyUnlockedChainFrameChanges()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx, locked: false);
        try
        {
            var lockedChain = new AnimationChainSave { Name = "Locked", IsLocked = true };
            var lockedFrame = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                FrameLength      = 0.1f,
                LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
                RightCoordinate  = 1.0f, BottomCoordinate = 0.5f,
                ShapesSave       = new ShapesSave(),
            };
            lockedChain.Frames.Add(lockedFrame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(lockedChain);
            ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object>
            {
                ctx.SelectedState.SelectedChain!, lockedChain,
            };
            ctrl.RefreshFrames();

            ctrl.SimulateBulkHandleDrag(frame, HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   8f);

            Assert.Equal(0.125f, frame.LeftCoordinate, precision: 4); // unlocked chain's frame moved
            Assert.Equal(0.5f, lockedFrame.LeftCoordinate, precision: 4); // locked chain's frame untouched
            Assert.Equal(0f, lockedFrame.TopCoordinate, precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
