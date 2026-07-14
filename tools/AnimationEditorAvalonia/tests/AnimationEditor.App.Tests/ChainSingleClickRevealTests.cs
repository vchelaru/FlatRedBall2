using System.Reflection;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression for a real bug missed by <see cref="WireframeSelectionRevealTests"/> (which set
/// <c>ISelectedState.SelectedChain</c> directly instead of driving a real tree click):
/// <c>MainWindow.OnTreeSelectionChanged</c> syncs the tree's <c>SelectedItems</c> into
/// <c>ISelectedState.SelectedNodes</c> on *every* selection change, including an ordinary single
/// click — so a plain chain click always puts that one chain into <c>SelectedChains</c> too
/// (count == 1), not just a genuine Ctrl/Shift multi-select. The first cut of
/// <c>WireframeControl.ComputeHighlightedFrames</c> treated any non-empty <c>SelectedChains</c>
/// as "multi-chain selection, don't highlight," which silently swallowed the single-click case:
/// the reveal timer still started (frame identity changed from the prior state), but every frame
/// rendered with <c>IsSelected=false</c>, so nothing visibly grew-then-shrank.
///
/// These tests drive real MouseDown/MouseUp clicks through the actual tree (not reflection or a
/// direct model assignment) so they exercise the real SelectedNodes/SelectedChain interaction.
/// </summary>
public class ChainSingleClickRevealTests
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
        bm.Erase(SKColors.Blue);
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

    /// <summary>
    /// Builds a two-frame chain, wires a loaded texture, and returns the window/context plus the
    /// chain's header label and frame-0's row (both real, realized tree controls to click on).
    /// </summary>
    private static (MainWindow Window, TestServices Ctx, TreeNodeVm ChainNode, Control ChainHeaderLabel,
        TreeViewItem FrameTvi, WireframeControl Wireframe, string Dir) BuildTwoFrameChainWindow()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var chain = new AnimationChainSave { Name = "Walk" };
        var f0 = new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.1f, TopCoordinate = 0.1f, RightCoordinate = 0.3f, BottomCoordinate = 0.3f };
        var f1 = new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.4f, TopCoordinate = 0.1f, RightCoordinate = 0.6f, BottomCoordinate = 0.3f };
        chain.Frames.Add(f0);
        chain.Frames.Add(f1);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

        var texPath = WriteSolidPng(dir, "tex.png", 1000, 1000);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        TriggerRefreshTreeView(window);

        var tree = window.FindControl<TreeView>("AnimTree")!;
        var chainNode = (TreeNodeVm)tree.ItemsSource!.Cast<object>().First();
        chainNode.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();

        var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
        wireframe.LoadTexture(texPath);
        Dispatcher.UIThread.RunJobs();

        var chainTvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, chainNode));
        var chainHeaderLabel = chainTvi.GetVisualDescendants().OfType<TextBlock>()
            .First(tb => ReferenceEquals(tb.DataContext, chainNode) && tb.Name == "RowHeaderLabel");

        var frameTvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, chainNode.Children[0]));

        return (window, ctx, chainNode, chainHeaderLabel, frameTvi, wireframe, dir);
    }

    [AvaloniaFact]
    public void SingleClick_ChainRow_FromFreshState_HighlightsAllFramesAndStartsReveal()
    {
        var (window, ctx, chainNode, chainHeaderLabel, _, wireframe, dir) = BuildTwoFrameChainWindow();
        try
        {
            RealSingleClick(window, chainHeaderLabel);

            Assert.True(wireframe.IsSelectionRevealAnimating,
                "Clicking a chain must start the shrink-to-rest reveal.");
            var rects = wireframe.GetFrameRects();
            Assert.Equal(2, rects.Count);
            Assert.All(rects, r => Assert.True(r.IsSelected,
                "Every frame of a single-clicked chain must draw with the blue highlight."));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    /// <summary>
    /// The exact repro reported against the previous fix: select a frame, then click back to the
    /// owning chain. Toggling must re-highlight and re-reveal every frame, not silently no-op.
    /// </summary>
    [AvaloniaFact]
    public void SingleClick_ChainRow_AfterFrameWasSelected_HighlightsAllFramesAndStartsReveal()
    {
        var (window, ctx, chainNode, chainHeaderLabel, frameTvi, wireframe, dir) = BuildTwoFrameChainWindow();
        try
        {
            RealSingleClick(window, frameTvi);
            wireframe.SettleSelectionReveal();
            Assert.NotNull(ctx.SelectedState.SelectedFrame);

            RealSingleClick(window, chainHeaderLabel);

            Assert.Null(ctx.SelectedState.SelectedFrame);
            Assert.True(wireframe.IsSelectionRevealAnimating,
                "Toggling from a frame back to its chain must restart the reveal.");
            var rects = wireframe.GetFrameRects();
            Assert.Equal(2, rects.Count);
            Assert.All(rects, r => Assert.True(r.IsSelected));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    /// <summary>Regression: toggling the other direction (chain -> frame) still isolates just that frame.</summary>
    [AvaloniaFact]
    public void SingleClick_FrameRow_AfterChainWasSelected_IsolatesThatFrame()
    {
        var (window, ctx, chainNode, chainHeaderLabel, frameTvi, wireframe, dir) = BuildTwoFrameChainWindow();
        try
        {
            RealSingleClick(window, chainHeaderLabel);
            wireframe.SettleSelectionReveal();

            RealSingleClick(window, frameTvi);

            Assert.Same(chainNode.Children[0].Data, ctx.SelectedState.SelectedFrame);
            var rects = wireframe.GetFrameRects();
            Assert.Single(rects);
            Assert.True(rects[0].IsSelected);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Re-clicking the *same already-selected* chain must still replay the reveal, not just a
    /// click that changes the selection. <c>WireframeControl.OnSelectionChanged</c> only restarts
    /// the reveal when the highlighted frame *set* differs from last time (via
    /// <c>SelectedFramesIdentityChanged</c>) — re-selecting the same chain reproduces the
    /// identical set, so that content-diff alone silently swallowed the replay. The real click
    /// site (<c>MainWindow.OnTreePointerPressed</c>) now calls
    /// <c>WireframeControl.ReplaySelectionReveal()</c> unconditionally instead of relying solely
    /// on the diff.
    /// </summary>
    [AvaloniaFact]
    public void SingleClick_SameChainTwice_ReplaysRevealBothTimes()
    {
        var (window, ctx, chainNode, chainHeaderLabel, _, wireframe, dir) = BuildTwoFrameChainWindow();
        try
        {
            RealSingleClick(window, chainHeaderLabel);
            Assert.True(wireframe.IsSelectionRevealAnimating, "First click must start the reveal.");
            wireframe.SettleSelectionReveal();

            // Sleep past Avalonia's double-click time window: two RealSingleClicks at the same
            // point back-to-back would otherwise register as ClickCount==2 (a double-click,
            // which does the focus gesture instead — see ChainRowDoubleTapFocusTests), not two
            // independent single clicks. A real user re-selecting the same row after browsing
            // elsewhere clicks well outside that window.
            System.Threading.Thread.Sleep(700);
            RealSingleClick(window, chainHeaderLabel);

            Assert.True(wireframe.IsSelectionRevealAnimating,
                "Re-clicking the same already-selected chain must replay the reveal.");
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    /// <summary>Same as above, for a frame — re-clicking the already-selected frame must replay too.</summary>
    [AvaloniaFact]
    public void SingleClick_SameFrameTwice_ReplaysRevealBothTimes()
    {
        var (window, ctx, chainNode, _, frameTvi, wireframe, dir) = BuildTwoFrameChainWindow();
        try
        {
            RealSingleClick(window, frameTvi);
            Assert.True(wireframe.IsSelectionRevealAnimating, "First click must start the reveal.");
            wireframe.SettleSelectionReveal();

            System.Threading.Thread.Sleep(700); // see comment in the chain test above.
            RealSingleClick(window, frameTvi);

            Assert.True(wireframe.IsSelectionRevealAnimating,
                "Re-clicking the same already-selected frame must replay the reveal.");
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Regression: switching from chain A to a *different* chain B must end with only chain B's
    /// frames highlighted — not a mix, and not chain A's frames caught mid-reveal. The original
    /// bug (<c>MainWindow.OnTreePointerPressed</c> calling <c>WireframeControl.ReplaySelectionReveal</c>
    /// unconditionally on every click) restarted the reveal on chain A's still-rendered frames a
    /// beat before the highlight moved to chain B — a visible flash of the wrong chain growing —
    /// because that call runs synchronously at Tunnel-phase PointerPressed, before the actual
    /// selection update and WireframeControl's SelectionChanged→RefreshFrames catch-up have run.
    /// Headless's <c>window.MouseDown</c>/<c>MouseUp</c> fully pump that catch-up before
    /// returning, so this test cannot freeze-frame the transient race the way a real 60fps render
    /// loop can — it guards the fix's actual mechanism instead: <c>ReplaySelectionReveal</c> must
    /// only fire when the clicked chain is *already* the selection (see the reference-equality
    /// guard at the chain-click site), so a switch to a genuinely different chain never resets
    /// the reveal until the new chain's frame set is the one being drawn.
    /// </summary>
    [AvaloniaFact]
    public void SingleClick_DifferentChain_EndsWithOnlyNewChainHighlighted()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var chainA = new AnimationChainSave { Name = "A" };
            var af0 = new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.1f, TopCoordinate = 0.1f, RightCoordinate = 0.3f, BottomCoordinate = 0.3f };
            chainA.Frames.Add(af0);
            var chainB = new AnimationChainSave { Name = "B" };
            var bf0 = new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.4f, TopCoordinate = 0.1f, RightCoordinate = 0.6f, BottomCoordinate = 0.3f };
            chainB.Frames.Add(bf0);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainA);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainB);
            var texPath = WriteSolidPng(dir, "tex.png", 1000, 1000);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            TriggerRefreshTreeView(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var nodes = tree.ItemsSource!.Cast<TreeNodeVm>().ToList();
            var chainANode = nodes.First(n => ReferenceEquals(n.Data, chainA));
            var chainBNode = nodes.First(n => ReferenceEquals(n.Data, chainB));

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            wireframe.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            Control HeaderLabelFor(TreeNodeVm node)
            {
                var tvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
                    .First(t => ReferenceEquals(t.DataContext, node));
                return tvi.GetVisualDescendants().OfType<TextBlock>()
                    .First(tb => ReferenceEquals(tb.DataContext, node) && tb.Name == "RowHeaderLabel");
            }

            RealSingleClick(window, HeaderLabelFor(chainANode));
            wireframe.SettleSelectionReveal();

            System.Threading.Thread.Sleep(700); // avoid ClickCount==2, see other tests' comments.
            RealSingleClick(window, HeaderLabelFor(chainBNode));

            Assert.Same(chainB, ctx.SelectedState.SelectedChain);
            var rects = wireframe.GetFrameRects();
            Assert.Single(rects);
            Assert.True(rects[0].IsSelected);
            Assert.True(wireframe.IsSelectionRevealAnimating,
                "Switching to a different chain must still start its reveal.");
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    /// <summary>
    /// The real-world repro reported live: a chain with exactly *one* frame. Selecting the whole
    /// chain and selecting its lone frame both compute to the identical one-frame highlighted set
    /// (<c>WireframeControl.ComputeHighlightedFrames</c>), so the content-based
    /// <c>SelectedFramesIdentityChanged</c> check alone sees "no change" and skips the reveal —
    /// even though the user genuinely clicked a different tree node (the chain, then its frame).
    /// The reveal must restart on the click target changing, not only on the resulting frame set
    /// changing.
    /// </summary>
    [AvaloniaFact]
    public void SingleClick_ChainThenItsOnlyFrame_StillReplaysReveal()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.1f, TopCoordinate = 0.1f, RightCoordinate = 0.3f, BottomCoordinate = 0.3f };
            chain.Frames.Add(f0);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            var texPath = WriteSolidPng(dir, "tex.png", 1000, 1000);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            TriggerRefreshTreeView(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = (TreeNodeVm)tree.ItemsSource!.Cast<object>().First();
            chainNode.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            wireframe.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            var chainTvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, chainNode));
            var chainHeaderLabel = chainTvi.GetVisualDescendants().OfType<TextBlock>()
                .First(tb => ReferenceEquals(tb.DataContext, chainNode) && tb.Name == "RowHeaderLabel");
            var frameTvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, chainNode.Children[0]));

            RealSingleClick(window, chainHeaderLabel);
            wireframe.SettleSelectionReveal();

            System.Threading.Thread.Sleep(700); // avoid ClickCount==2, see other tests' comments.
            RealSingleClick(window, frameTvi);

            Assert.Same(f0, ctx.SelectedState.SelectedFrame);
            Assert.True(wireframe.IsSelectionRevealAnimating,
                "Clicking the chain's only frame after the chain was selected must replay the " +
                "reveal, even though the highlighted frame set (just that one frame either way) " +
                "didn't change.");
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
