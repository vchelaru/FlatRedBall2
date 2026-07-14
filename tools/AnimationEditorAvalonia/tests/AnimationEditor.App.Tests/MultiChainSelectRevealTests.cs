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
/// A multi-frame selection (Ctrl+click 2+ frames) already highlights and reveals every selected
/// frame (issue #582 / #542). A multi-*chain* selection (Ctrl+click 2+ chains) drew every frame
/// of every selected chain but never marked any of them <c>IsSelected</c> — so the frames rendered
/// hard/static (translucent, no reveal), inconsistent with how a single chain or a multi-frame
/// selection already behave. This makes multi-chain selection highlight and reveal the union of
/// all selected chains' frames too, the same as those other cases.
/// </summary>
public class MultiChainSelectRevealTests
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
    public void CtrlClickTwoChains_HighlightsAndRevealsAllTheirFrames()
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

            Assert.Equal(2, ctx.SelectedState.SelectedChains.Count);
            Assert.True(wireframe.IsSelectionRevealAnimating,
                "Ctrl+clicking a second chain must restart the reveal for the whole multi-chain selection.");
            var rects = wireframe.GetFrameRects();
            Assert.Equal(2, rects.Count);
            Assert.All(rects, r => Assert.True(r.IsSelected,
                "Every frame across a multi-chain selection must draw with the blue highlight, " +
                "the same as a single-chain or multi-frame selection."));
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
