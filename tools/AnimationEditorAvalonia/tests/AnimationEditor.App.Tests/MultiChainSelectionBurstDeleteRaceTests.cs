using System.Reflection;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1172: a rapid multi-row tree selection (Ctrl+click several chains, Shift+click a range,
/// Shift+Arrow extension) can fire Avalonia's <c>TreeView.SelectionChanged</c> once per row (see
/// <see cref="MainWindow"/>'s burst-coalescing comment on
/// <c>OnTreeSelectionChanged</c>/<c>EndTreeSelectionBurst</c>). The leading row syncs
/// <see cref="ISelectedState.SelectedNodes"/> immediately; every following row in the same burst is
/// buffered and only synced by a trailing <c>Dispatcher.UIThread.Post</c> callback. If Delete fires
/// before that trailing callback has run, <c>HandleDelete</c> would read a stale, incomplete
/// <c>SelectedChains</c> (only the first-clicked chain) and delete just that one -- and the trailing
/// sync would then still run afterward and re-populate <c>SelectedNodes</c> from the tree's current
/// (still fully multi-selected) <c>AnimTree.SelectedItems</c>, resurrecting the chains the user
/// believed they'd just deleted back into the selection, so the wireframe draws their frames again.
/// This drives the tree's own multi-selection collection directly (not simulated pointer input,
/// which the Avalonia.Headless click helpers fully pump before returning, masking the race) so the
/// burst is left genuinely mid-flight the way a real event-loop interleaving could leave it, then
/// calls the delete path the same way a real keypress or context-menu click would.
/// </summary>
public class MultiChainSelectionBurstDeleteRaceTests
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

    [AvaloniaFact]
    public void DeleteMidSelectionBurst_DoesNotResurrectChainsIntoSelectionAfterDelete()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var chainA = new AnimationChainSave { Name = "A" };
            chainA.Frames.Add(new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.1f, TopCoordinate = 0.1f, RightCoordinate = 0.3f, BottomCoordinate = 0.3f });
            var chainB = new AnimationChainSave { Name = "B" };
            chainB.Frames.Add(new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.4f, TopCoordinate = 0.1f, RightCoordinate = 0.6f, BottomCoordinate = 0.3f });
            var chainC = new AnimationChainSave { Name = "C" };
            chainC.Frames.Add(new AnimationFrameSave { TextureName = "tex.png", LeftCoordinate = 0.7f, TopCoordinate = 0.1f, RightCoordinate = 0.9f, BottomCoordinate = 0.3f });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainA);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainB);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chainC);
            var texPath = WriteSolidPng(dir, "tex.png", 1000, 1000);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            TriggerRefreshTreeView(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var nodes = tree.ItemsSource!.Cast<TreeNodeVm>().ToList();
            var nodeA = nodes.First(n => ReferenceEquals(n.Data, chainA));
            var nodeB = nodes.First(n => ReferenceEquals(n.Data, chainB));
            var nodeC = nodes.First(n => ReferenceEquals(n.Data, chainC));

            var wireframe = window.FindControl<WireframeControl>("WireframeCtrl")!;
            wireframe.LoadTexture(texPath);
            Dispatcher.UIThread.RunJobs();

            // Simulate a rapid Ctrl+click burst across all three rows WITHOUT letting the
            // dispatcher settle between rows -- exactly what SelectionChanged firing once per
            // row (per OnTreeSelectionChanged's own doc comment) looks like faster than a
            // render frame. tree.SelectedItems is Avalonia's own multi-selection collection.
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(nodeA); // leading edge: synced immediately
            tree.SelectedItems.Add(nodeB); // buffered -- posted trailing sync only
            tree.SelectedItems.Add(nodeC); // buffered -- posted trailing sync only

            // Delete fires before the trailing sync (Dispatcher.UIThread.Post) has run.
            window.HandleDeleteForTest();

            // Now let the trailing sync (and everything else queued) settle.
            Dispatcher.UIThread.RunJobs();

            var remaining = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
            Assert.DoesNotContain(remaining, c => ReferenceEquals(c, chainA));
            Assert.DoesNotContain(remaining, c => ReferenceEquals(c, chainB));
            Assert.DoesNotContain(remaining, c => ReferenceEquals(c, chainC));

            Assert.Empty(wireframe.GetFrameRects());
            Assert.DoesNotContain(ctx.SelectedState.SelectedNodes, n => ReferenceEquals(n, chainB) || ReferenceEquals(n, chainC));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
