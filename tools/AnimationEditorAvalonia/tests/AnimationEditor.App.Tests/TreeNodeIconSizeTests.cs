using System.Reflection;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #261: node-kind icons in the animation TreeView were a fixed 14×14 inside a
/// 32px-tall row, leaving large empty bands above and below. The icon should grow to
/// use that space — but the row itself must NOT get taller.
/// </summary>
public class TreeNodeIconSizeTests
{
    /// <summary>Avalonia Fluent's <c>TreeViewItemMinHeight</c> — the row height we must not exceed.</summary>
    private const double RowHeight = 32;

    private static (MainWindow Window, TestServices Ctx) CreateWindowWithChainAndFrame()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png" });
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

        typeof(MainWindow)
            .GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();

        // Force a full layout pass so Bounds are populated.
        window.Measure(new Size(1600, 900));
        window.Arrange(new Rect(0, 0, 1600, 900));
        Dispatcher.UIThread.RunJobs();

        return (window, ctx);
    }

    /// <summary>Icon SVG filenames used for the node-kind glyphs (not the inline "+" add-frame button).</summary>
    private static readonly string[] NodeIconFiles =
        ["IconChain.svg", "IconFrame.svg", "IconShape.svg", "IconCircle.svg"];

    /// <summary>The visible node-kind icons (chain glyph, frame glyph) drawn from the icon SVG set.</summary>
    private static List<Avalonia.Svg.Skia.Svg> NodeIconSvgs(MainWindow window)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        return tree.GetVisualDescendants()
            .OfType<Avalonia.Svg.Skia.Svg>()
            .Where(s => s.Path is not null && NodeIconFiles.Any(f => s.Path.EndsWith(f)))
            .Where(s => s.Bounds.Width > 0)   // skip the IsVisible=false template branches
            .ToList();
    }

    [AvaloniaFact]
    public void NodeIcons_AreLargerThanLegacy14px()
    {
        var (window, _) = CreateWindowWithChainAndFrame();
        try
        {
            var icons = NodeIconSvgs(window);
            Assert.NotEmpty(icons);
            foreach (var svg in icons)
            {
                Assert.True(svg.Width >= 20,
                    $"Node icon width {svg.Width} should grow well past the old 14px to use the row's empty space.");
                Assert.Equal(svg.Width, svg.Height);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NodeIcons_StillFitWithinRowHeight()
    {
        var (window, _) = CreateWindowWithChainAndFrame();
        try
        {
            foreach (var svg in NodeIconSvgs(window))
                Assert.True(svg.Height <= RowHeight,
                    $"Node icon height {svg.Height} must not exceed the {RowHeight}px row height.");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ChainThumbnail_IsBakedAtLeastAtDisplaySize_SoItIsNotUpscaledAndBlurry()
    {
        // Regression: the chain first-frame thumbnail used to be baked at 14x14 and then
        // displayed at the (now larger) icon size, so the Image control upscaled it — blurry.
        // It must be baked at no smaller than the displayed icon size.
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var pngPath = Path.Combine(dir, "red.png");
            using (var bm = new SKBitmap(64, 64))
            {
                bm.Erase(SKColors.Red);
                using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(pngPath, data.ToArray());
            }
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName     = "red.png",
                LeftCoordinate  = 0f, TopCoordinate    = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
            });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            typeof(MainWindow)
                .GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, null);
            Dispatcher.UIThread.RunJobs();

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = ((System.Collections.ObjectModel.ObservableCollection<TreeNodeVm>)tree.ItemsSource!)[0];
            var thumbnail = Assert.IsType<Avalonia.Media.Imaging.Bitmap>(chainNode.Thumbnail);
            Assert.True(thumbnail.PixelSize.Width >= 28,
                $"Thumbnail baked at {thumbnail.PixelSize.Width}px — must be >= the 28px display size so it is downsampled, not upscaled.");
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void EnlargingIcons_DoesNotMakeTreeRowsTaller()
    {
        var (window, _) = CreateWindowWithChainAndFrame();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;

            // Leaf rows (frame nodes) have no nested TreeViewItem, so their Bounds height
            // is exactly one row — it must stay at the Fluent default, not grow with the icon.
            var leafItems = tree.GetVisualDescendants()
                .OfType<TreeViewItem>()
                .Where(tvi => !tvi.GetVisualDescendants().OfType<TreeViewItem>().Any())
                .ToList();

            Assert.NotEmpty(leafItems);
            foreach (var tvi in leafItems)
                Assert.Equal(RowHeight, tvi.Bounds.Height);
        }
        finally { window.Close(); }
    }
}
