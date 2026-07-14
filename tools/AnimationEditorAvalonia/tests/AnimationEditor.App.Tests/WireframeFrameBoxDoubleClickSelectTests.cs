using System.Reflection;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #719: when a whole animation (chain) is selected, the wireframe panel shows every
/// one of its frame boxes overlaid. Double-clicking one of those boxes must select the
/// corresponding frame — same result as clicking it in the tree view.
///
/// Before the fix, a click landing inside a frame box while the *chain* (not a single frame)
/// is selected is claimed by <c>WireframeControl.HitTestHandle</c>'s composite "drag the whole
/// chain" fallback (any point inside any of the chain's frame boxes hit-tests as
/// <c>HandleKind.Move</c> when no single frame is selected), so
/// <c>TrySelectFrameAtPoint</c> is never reached and the double-click silently starts/re-arms a
/// chain drag instead of selecting the frame under the cursor.
///
/// These tests drive real MouseDown/MouseUp pairs (not reflection or direct
/// <c>ISelectedState</c> assignment) so they exercise the actual pointer-routing precedence
/// between chain-drag hit-testing and frame selection — see
/// .claude/skills/animation-editor-testing/SKILL.md for why reflection alone would miss this
/// class of routing bug.
/// </summary>
public class WireframeFrameBoxDoubleClickSelectTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static void TriggerRefreshTreeView(MainWindow window)
    {
        typeof(MainWindow).GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static string WriteSolidPng(string dir, string name, int width, int height)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(width, height);
        bm.Erase(SKColors.Black);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static void RealSingleClick(MainWindow window, Control target)
    {
        var local = new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
        var p = target.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Simulates a real double-click (two MouseDown/MouseUp pairs) at a specific
    /// point local to <paramref name="target"/>, driving the actual pointer pipeline.</summary>
    private static void RealDoubleClickAt(MainWindow window, Control target, Point local)
    {
        var p = target.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Builds a two-frame chain ("A" at pixel (10,10)-(30,30), "B" at (60,60)-(80,80) on a
    /// 100x100 texture), selects the *chain* (not either frame) via a real tree click so both
    /// boxes render in the wireframe, and returns the window/context plus both frames and the
    /// realized WireframeControl with camera fixed at pan=(0,0) zoom=1 (screen ≡ texture pixels).
    /// </summary>
    private static (MainWindow Window, TestServices Ctx, AnimationFrameSave FrameA,
        AnimationFrameSave FrameB, WireframeControl Wireframe, string Dir) BuildChainSelectedWindow()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var chain = new AnimationChainSave { Name = "Walk" };
        var frameA = new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 10f / 100f, TopCoordinate = 10f / 100f, RightCoordinate = 30f / 100f, BottomCoordinate = 30f / 100f };
        var frameB = new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 60f / 100f, TopCoordinate = 60f / 100f, RightCoordinate = 80f / 100f, BottomCoordinate = 80f / 100f };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

        var texPath = WriteSolidPng(dir, "tex.png", 100, 100);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        TriggerRefreshTreeView(window);

        var tree = window.FindControl<TreeView>("AnimTree")!;
        var chainNode = (TreeNodeVm)tree.ItemsSource!.Cast<object>().First();
        chainNode.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
        wireframe.LoadTexture(texPath);
        wireframe.SetCamera(0, 0, 1);   // pan=(0,0), zoom=1 → screen ≡ texture pixel coordinates
        Dispatcher.UIThread.RunJobs();

        var chainTvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, chainNode));
        var chainHeaderLabel = chainTvi.GetVisualDescendants().OfType<TextBlock>()
            .First(tb => ReferenceEquals(tb.DataContext, chainNode) && tb.Name == "RowHeaderLabel");

        // Select the whole chain (not a single frame) so both frame boxes render at once —
        // the exact scenario issue #719 describes.
        RealSingleClick(window, chainHeaderLabel);
        Thread.Sleep(700); // outrun Avalonia's double-click window before the real gesture under test.
        Assert.Null(ctx.SelectedState.SelectedFrame);
        Assert.Equal(2, wireframe.GetFrameRects().Count);

        // Selecting the chain can trigger a camera fit/reveal animation — pin the camera back
        // to pan=(0,0) zoom=1 *after* selection so screen == texture pixel coordinates for the
        // double-click under test.
        wireframe.SetCamera(0, 0, 1);
        Dispatcher.UIThread.RunJobs();

        return (window, ctx, frameA, frameB, wireframe, dir);
    }

    [AvaloniaFact]
    public void DoubleClick_FrameBoxOfSelectedChain_SelectsThatFrame()
    {
        var (window, ctx, frameA, frameB, wireframe, dir) = BuildChainSelectedWindow();
        try
        {
            // Frame B's box spans screen (60,60)-(80,80) at camera(0,0,1); double-click its centre.
            RealDoubleClickAt(window, wireframe, new Point(70, 70));

            Assert.Same(frameB, ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void DoubleClick_OtherFrameBoxOfSelectedChain_SelectsThatOtherFrame()
    {
        var (window, ctx, frameA, frameB, wireframe, dir) = BuildChainSelectedWindow();
        try
        {
            // Frame A's box spans screen (10,10)-(30,30); double-click its centre.
            RealDoubleClickAt(window, wireframe, new Point(20, 20));

            Assert.Same(frameA, ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Double-clicking a spot with no frame box under it must not select or throw — it's
    /// simply not a hit, and existing gestures (e.g. chain-drag) are free to handle it.
    /// </summary>
    [AvaloniaFact]
    public void DoubleClick_EmptySpaceOfSelectedChain_DoesNotSelectAnyFrame()
    {
        var (window, ctx, frameA, frameB, wireframe, dir) = BuildChainSelectedWindow();
        try
        {
            RealDoubleClickAt(window, wireframe, new Point(45, 45)); // between the two boxes

            Assert.Null(ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
