using System.Collections.ObjectModel;
using System.Reflection;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless Avalonia tests for issue #236: a chain's tree-node icon shows a thumbnail
/// of its first frame, falling back to the generic chain icon when the chain is empty,
/// and regenerating when the first frame's visual changes.
/// </summary>
public class TreeThumbnailTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    private static ObservableCollection<TreeNodeVm> GetRoots(MainWindow w)
    {
        var tree = w.FindControl<TreeView>("AnimTree")
            ?? throw new InvalidOperationException("AnimTree control not found");
        return (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
    }

    private static void TriggerRefreshTreeView(MainWindow window)
    {
        var method = typeof(MainWindow).GetMethod(
            "RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RefreshTreeView not found");
        method.Invoke(window, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static string WriteSolidPng(string dir, string name, SKColor color, int size = 16)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>A frame whose full texture is the thumbnail source (UV region 0,0 → 1,1).</summary>
    private static AnimationFrameSave FullFrame(string textureName) => new()
    {
        TextureName     = textureName,
        LeftCoordinate  = 0f, TopCoordinate    = 0f,
        RightCoordinate = 1f, BottomCoordinate = 1f,
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void AnimationChainsChanged_AfterFirstFrameFlipHorizontal_RegeneratesTreeThumbnail()
    {
        // Flipping the first frame fires AnimationChainsChanged. The tree thumbnail for the
        // chain must regenerate to show the flipped version, not the stale cached one.
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteSolidPng(dir, "red.png", SKColors.Red);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(FullFrame("red.png"));
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            var chainNode = GetRoots(window)[0];
            var initialThumbnail = chainNode.Thumbnail;
            Assert.NotNull(initialThumbnail);

            // Flip the first frame and notify the system via the event bus (the same path
            // taken by FlipFrameHorizontally and undo/redo).
            chain.Frames[0].FlipHorizontal = true;
            ctx.ApplicationEvents.RaiseAnimationChainsChanged();
            Dispatcher.UIThread.RunJobs();

            Assert.NotSame(initialThumbnail, chainNode.Thumbnail);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void RefreshTreeView_ChainWithFirstFrame_SetsChainNodeThumbnail()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteSolidPng(dir, "red.png", SKColors.Red);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(FullFrame("red.png"));
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);

            var chainNode = GetRoots(window)[0];
            Assert.NotNull(chainNode.Thumbnail);
            Assert.True(chainNode.HasThumbnail);
            Assert.False(chainNode.ShowGenericChainIcon);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void RefreshTreeView_ChainWithNoFrames_LeavesChainNodeThumbnailNull()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Empty" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);

            var chainNode = GetRoots(window)[0];
            Assert.Null(chainNode.Thumbnail);
            // No first frame → the tree must show the generic chain glyph.
            Assert.True(chainNode.ShowGenericChainIcon);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RefreshTreeView_AfterFirstFrameTextureChange_RegeneratesThumbnail()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteSolidPng(dir, "red.png",  SKColors.Red);
            WriteSolidPng(dir, "blue.png", SKColors.Blue);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var firstFrame = FullFrame("red.png");
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(firstFrame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);
            var chainNode = GetRoots(window)[0];
            var firstThumbnail = chainNode.Thumbnail;
            Assert.NotNull(firstThumbnail);

            // Swap the first frame's texture — the icon must regenerate.
            firstFrame.TextureName = "blue.png";
            TriggerRefreshTreeView(window);

            Assert.NotNull(chainNode.Thumbnail);
            Assert.NotSame(firstThumbnail, chainNode.Thumbnail);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
