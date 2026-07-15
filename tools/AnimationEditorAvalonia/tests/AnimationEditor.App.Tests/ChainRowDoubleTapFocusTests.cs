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
/// Issue #716: double-clicking blank row space on a chain (not its label, not the Add-Frame
/// button) used to collapse/expand the row instead of doing anything useful. The DoubleTapped
/// RoutedEvent is not a reliable place to fix this: Avalonia's TreeViewItem toggles IsExpanded
/// from its own Tunnel-phase pointer handling on the *second* click of a double-click, before a
/// Bubble-registered DoubleTapped handler on an ancestor like AnimTree ever sees the gesture — so
/// a fix routed only through OnAnimTreeDoubleTapped's chain case is unreachable in practice. The
/// real fix lives in MainWindow.OnTreePointerPressed's existing Tunnel-phase ClickCount==2 branch
/// (the same place frame double-click-to-center already worked reliably): double-click's only job
/// is moving the camera onto the chain's frames via WireframeControl.FitChainToView.
///
/// Selecting/highlighting every frame (so they draw with the shrink-to-rest reveal, #542) is a
/// *single*-click concern instead, and needs no MainWindow-specific wiring at all: an ordinary
/// tree click already sets ISelectedState.SelectedChain, and WireframeControl.ComputeHighlightedFrames
/// treats a whole-chain selection as "every one of its frames is highlighted" — see
/// WireframeSelectionRevealTests for that half. Reusing SelectedNodes for this (the previous
/// approach) would have clobbered the chain multi-select bag on every plain click.
///
/// These tests drive real MouseDown/MouseUp double-clicks (not reflection) so they exercise the
/// actual pointer routing, not just the isolated dispatch method — see the postmortem in
/// .claude/skills/animation-editor-testing/SKILL.md for why reflection alone missed this class of bug.
/// </summary>
public class ChainRowDoubleTapFocusTests
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

    private static TreeView GetTree(MainWindow w)
        => w.FindControl<TreeView>("AnimTree")
           ?? throw new InvalidOperationException("AnimTree control not found");

    private static void TriggerRefreshTreeView(MainWindow window)
    {
        typeof(MainWindow).GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>FitChainToView no-ops without a loaded texture, so tests that need it to actually
    /// move the camera load a throwaway solid-color PNG first.</summary>
    private static string WriteSolidPng(string dir, string name, int width, int height)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(width, height);
        bm.Erase(SKColors.Blue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>The row's "Meta" label (frame count / duration) — genuine blank row space that
    /// isn't the rename label and isn't the hover-only Add-Frame button.</summary>
    private static TextBlock GetMetaLabel(TreeViewItem tvi, TreeNodeVm chainNode) =>
        tvi.GetVisualDescendants().OfType<TextBlock>()
            .First(tb => ReferenceEquals(tb.DataContext, chainNode) && tb.Name != "RowHeaderLabel");

    private static TextBlock GetHeaderLabel(TreeViewItem tvi, TreeNodeVm chainNode) =>
        tvi.GetVisualDescendants().OfType<TextBlock>()
            .First(tb => ReferenceEquals(tb.DataContext, chainNode) && tb.Name == "RowHeaderLabel");

    /// <summary>Simulates a real double-click (two MouseDown/MouseUp pairs) centred on
    /// <paramref name="target"/>, driving the actual pointer pipeline rather than reflection.</summary>
    private static void RealDoubleClick(MainWindow window, Control target)
    {
        var local = new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
        var p = target.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void DoubleClickBlankRowSpace_OnChainWithFrames_FitsChainInsteadOfCollapsing()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave
            {
                LeftCoordinate = 0.1f, TopCoordinate = 0.1f,
                RightCoordinate = 0.3f, BottomCoordinate = 0.3f,
            });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);

            var tree = GetTree(window);
            var chainNode = (TreeNodeVm)tree.ItemsSource!.Cast<object>().First();

            var tvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, chainNode));
            var metaLabel = GetMetaLabel(tvi, chainNode);

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")
                ?? throw new InvalidOperationException("WireframeCtrl not found");
            var texPath = WriteSolidPng(dir, "tex.png", 1000, 1000);
            wireframe.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();
            wireframe.SetCamera(0f, 0f, 1f);

            RealDoubleClick(window, metaLabel);

            // Whether Avalonia's own TreeViewItem also toggles IsExpanded alongside this is not
            // asserted either way — the user explicitly said that side effect is tolerable as
            // long as focus happens too (#716).
            Assert.False(chainNode.IsEditing,
                "Double-click on blank row space must not start an inline rename.");
            Assert.Same(chain, ctx.SelectedState.SelectedChain);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void DoubleClickHeaderLabel_OnChain_StillStartsRename()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            TriggerRefreshTreeView(window);

            var tree = GetTree(window);
            var chainNode = (TreeNodeVm)tree.ItemsSource!.Cast<object>().First();

            var tvi = tree.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, chainNode));
            var label = GetHeaderLabel(tvi, chainNode);

            // Real pointer double-clicks don't reliably synthesize the DoubleTapped gesture in
            // the headless test harness (see class remarks), so this specific assertion — that
            // rename is still reachable via the label's own DoubleTapped wiring, which this
            // change does not touch — is verified by invoking OnHeaderTextDoubleTapped directly,
            // mirroring the existing regression tests in HeadlessTreeViewTests.
            var fakeArgs = (TappedEventArgs)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(TappedEventArgs));
            fakeArgs.Source = label;

            var handler = typeof(MainWindow).GetMethod(
                "OnHeaderTextDoubleTapped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("OnHeaderTextDoubleTapped not found");
            handler.Invoke(window, [label, fakeArgs]);
            Dispatcher.UIThread.RunJobs();

            Assert.True(chainNode.IsEditing,
                "Double-click on the chain's label must still start an inline rename.");
        }
        finally { window.Close(); }
    }
}
