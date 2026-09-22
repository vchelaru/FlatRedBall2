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
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1172: Ctrl+click-selecting several animations (chains) in the tree draws every
/// selected chain's frames on the wireframe sheet (see <see cref="MultiChainSelectRevealTests"/>).
/// Deleting that whole multi-selection at once must clear all of those frame rects from the
/// sheet, mirroring a real user's Ctrl+click-then-Delete gesture rather than a direct
/// <c>SelectedState</c> assignment (see the animation-editor-testing skill's note on real clicks
/// having side effects a direct assignment skips).
/// </summary>
public class MultiChainDeleteClearsOverlayTests
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

    private static void Click(MainWindow window, Control target, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var local = new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
        var p = target.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left, modifiers);
        window.MouseUp(p, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void DeleteAfterCtrlClickTwoChains_ClearsWireframeOverlay()
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

            Click(window, HeaderLabelFor(chainANode));
            wireframe.SettleSelectionReveal();
            Click(window, HeaderLabelFor(chainBNode), RawInputModifiers.Control);

            Assert.Equal(2, wireframe.GetFrameRects().Count); // baseline: both chains' frames drawn

            window.HandleDeleteForTest();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(wireframe.GetFrameRects());
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
