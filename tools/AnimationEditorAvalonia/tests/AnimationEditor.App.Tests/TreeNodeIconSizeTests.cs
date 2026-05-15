using System.Collections.ObjectModel;
using System.Reflection;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #261: the chain ("animation") icon in the TreeView — the first-frame preview
/// thumbnail, or the generic chain glyph when a chain has no frames — should be enlarged
/// to fill the empty space in the 32px row. Frame and shape icons are deliberately left
/// at the compact 14px size, and no row may grow taller.
/// </summary>
public class TreeNodeIconSizeTests
{
    /// <summary>Avalonia Fluent's <c>TreeViewItemMinHeight</c> — the row height we must not exceed.</summary>
    private const double RowHeight = 32;

    /// <summary>The compact icon size frame and shape glyphs keep (unchanged by #261).</summary>
    private const double CompactIconSize = 14;

    /// <summary>
    /// Builds a window whose tree is one chain → one frame → a rect + a circle, with every
    /// node expanded and a full layout pass done so each icon has real <c>Bounds</c>.
    /// The frame's texture deliberately does not resolve, so the chain row shows the generic
    /// chain glyph (the enlarged icon under test) rather than a baked thumbnail.
    /// </summary>
    private static MainWindow CreateWindowWithFullTree()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();

        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        frame.ShapesSave.Shapes.Add(new AARectSave { Name = "Box" });
        frame.ShapesSave.Shapes.Add(new CircleSave { Name = "Bounds" });
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

        typeof(MainWindow)
            .GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();

        ExpandAll(window);
        Dispatcher.UIThread.RunJobs();

        // Force a full layout pass so Bounds are populated.
        window.Measure(new Size(1600, 900));
        window.Arrange(new Rect(0, 0, 1600, 900));
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void ExpandAll(MainWindow window)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
        static void Recurse(TreeNodeVm n)
        {
            n.IsExpanded = true;
            foreach (var c in n.Children) Recurse(c);
        }
        foreach (var r in roots) Recurse(r);
    }

    /// <summary>The single realized, visible icon SVG whose path ends with the given filename.</summary>
    private static Avalonia.Svg.Skia.Svg IconSvg(MainWindow window, string fileName)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        return tree.GetVisualDescendants()
            .OfType<Avalonia.Svg.Skia.Svg>()
            .Where(s => s.Path is not null && s.Path.EndsWith(fileName))
            .Single(s => s.Bounds.Width > 0);   // the others are IsVisible=false template branches
    }

    [AvaloniaFact]
    public void ChainIcon_IsEnlargedPastLegacy14px()
    {
        var window = CreateWindowWithFullTree();
        try
        {
            var chainIcon = IconSvg(window, "IconChain.svg");
            Assert.True(chainIcon.Width >= 20,
                $"Chain icon width {chainIcon.Width} should grow past the old 14px to use the row's empty space.");
            Assert.Equal(chainIcon.Width, chainIcon.Height);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ChainIcon_StillFitsWithinRowHeight()
    {
        var window = CreateWindowWithFullTree();
        try
        {
            Assert.True(IconSvg(window, "IconChain.svg").Height <= RowHeight,
                $"Chain icon must not exceed the {RowHeight}px row height.");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void FrameAndShapeIcons_StayAtCompactSize()
    {
        // #261 asked for ONLY the animation (chain) icon to grow. Frame and shape icons
        // must stay at the original compact size.
        var window = CreateWindowWithFullTree();
        try
        {
            foreach (var file in new[] { "IconFrame.svg", "IconShape.svg", "IconCircle.svg" })
            {
                var icon = IconSvg(window, file);
                Assert.Equal(CompactIconSize, icon.Width);
                Assert.Equal(CompactIconSize, icon.Height);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EnlargingChainIcon_DoesNotMakeAnyRowTaller()
    {
        var window = CreateWindowWithFullTree();
        try
        {
            var tree = window.FindControl<TreeView>("AnimTree")!;

            // The tree has exactly 4 rows: chain → frame → (rect, circle). The root chain
            // TreeViewItem's Bounds span the whole subtree. Every row has a 32px MinHeight,
            // so if the total is exactly 4×32 then no individual header grew with the icon.
            var rootItem = tree.GetVisualDescendants()
                .OfType<TreeViewItem>()
                .Single(tvi => tvi.DataContext is TreeNodeVm { IsChainNode: true });

            Assert.Equal(RowHeight * 4, rootItem.Bounds.Height);
        }
        finally { window.Close(); }
    }

    // The "chain thumbnail is baked at >= the display size, not tiny-then-upscaled"
    // regression is covered deterministically by the pure [Fact]
    // ThumbnailServiceTests.RenderFrameThumbnail_SquareSource_BakesAtTheRequestedSize.
    // It is not re-tested through a full headless window here: that path decodes the
    // texture file through MainWindow/WireframeControl code the test cannot stub, so a
    // synthetic fixture is unreliable on the Linux CI runner (the flakiness #261's
    // d0b4c7a already fought). RefreshTreeThumbnails passing the TreeChainThumbnailPixelSize
    // constant is thin wiring left untested by design.
}
