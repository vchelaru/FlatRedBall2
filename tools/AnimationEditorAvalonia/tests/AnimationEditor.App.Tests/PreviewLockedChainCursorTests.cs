using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A locked chain (issue #1032) must not show any drag/resize affordance in the Preview panel --
/// the drag itself is already guarded (see <see cref="PreviewLockedChainDragTests"/>), but the
/// hover cursor must not imply a locked shape or frame is draggable either.
/// </summary>
public class PreviewLockedChainCursorTests
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

    private static string WriteSolidPng(string dir, string name = "sprite.png", int size = 64)
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.CornflowerBlue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static (PreviewControl preview, MainWindow window, float centerX, float centerY) ShowPreview(TestServices ctx)
    {
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
        float centerX = (float)((preview.Bounds.Width - 20) / 2 + 20);
        float centerY = (float)((preview.Bounds.Height - 20) / 2 + 20);
        return (preview, window, centerX, centerY);
    }

    // ── Shape resize handle ────────────────────────────────────────────────────

    private static AnimationFrameSave BuildFrameWithRect(TestServices ctx, bool locked, out AARectSave rect)
    {
        rect = new AARectSave { X = 0f, Y = 0f, ScaleX = 10f, ScaleY = 10f };
        var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Shapes.Add(rect);
        var chain = new AnimationChainSave { Name = "Test", IsLocked = locked };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedRectangle = rect;
        return frame;
    }

    [AvaloniaFact]
    public void GetHoverCursorTypeForTest_ShapeHandleInLockedChain_ReturnsNull()
    {
        var ctx = ResetSingletons();
        BuildFrameWithRect(ctx, locked: true, out _);
        var (preview, window, centerX, centerY) = ShowPreview(ctx);

        // TopLeft handle of a 10x10-half-extent rect at world origin, om=1: (cx-15, cy-15).
        Assert.Null(preview.GetHoverCursorTypeForTest(centerX - 15f, centerY - 15f));

        window.Close();
    }

    [AvaloniaFact]
    public void GetHoverCursorTypeForTest_ShapeHandleInUnlockedChain_ReturnsResizeCursor()
    {
        var ctx = ResetSingletons();
        BuildFrameWithRect(ctx, locked: false, out _);
        var (preview, window, centerX, centerY) = ShowPreview(ctx);

        Assert.Equal(StandardCursorType.TopLeftCorner,
            preview.GetHoverCursorTypeForTest(centerX - 15f, centerY - 15f));

        window.Close();
    }

    // ── Frame sprite (whole-frame drag) ────────────────────────────────────────

    [AvaloniaFact]
    public void GetHoverCursorTypeForTest_FrameSpriteInLockedChain_ReturnsNull()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir);
            var frame = new AnimationFrameSave
            {
                TextureName = texPath, FrameLength = 0.1f,
                RelativeX = 0f, RelativeY = 0f, ShapesSave = new ShapesSave(),
            };
            var chain = new AnimationChainSave { Name = "Test", IsLocked = true };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            ctx.ThumbnailService.GetBitmap(texPath);

            var (preview, window, centerX, centerY) = ShowPreview(ctx);

            Assert.Null(preview.GetHoverCursorTypeForTest(centerX, centerY));

            window.Close();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void GetHoverCursorTypeForTest_FrameSpriteInUnlockedChain_ReturnsSizeAll()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir);
            var frame = new AnimationFrameSave
            {
                TextureName = texPath, FrameLength = 0.1f,
                RelativeX = 0f, RelativeY = 0f, ShapesSave = new ShapesSave(),
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            ctx.ThumbnailService.GetBitmap(texPath);

            var (preview, window, centerX, centerY) = ShowPreview(ctx);

            Assert.Equal(StandardCursorType.SizeAll, preview.GetHoverCursorTypeForTest(centerX, centerY));

            window.Close();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
