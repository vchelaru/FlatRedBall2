using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless app tests for issue #498 multi-select copy/paste/duplicate routing.
/// </summary>
public class MultiCopyPasteAppTests
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

    private static void RebuildTree(MainWindow window)
    {
        typeof(MainWindow).GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object[] { Array.Empty<string>() });
        FlushUi();
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static TreeNodeVm FirstChainNode(TreeView tree) =>
        tree.ItemsSource is System.Collections.IEnumerable roots
            ? roots.Cast<TreeNodeVm>().First()
            : throw new Xunit.Sdk.XunitException("No tree roots");

    [AvaloniaFact]
    public async Task Copy_ThreeFrames_PutsAllOnClipboard()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
            var f1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f };
            var f2 = new AnimationFrameSave { TextureName = "c.png", FrameLength = 0.1f };
            chain.Frames.AddRange(new[] { f0, f1, f2 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = FirstChainNode(tree);
            var frameNodes = chainNode.Children;

            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(frameNodes[2]);
            tree.SelectedItems.Add(frameNodes[0]);
            tree.SelectedItems.Add(frameNodes[1]);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            var clip = await window.Clipboard!.TryGetTextAsync();
            Assert.True(ClipboardPayload.TryDeserialize(clip!, out _, out var frames, out _, out _));
            Assert.Equal(3, frames!.Count);
            Assert.Equal(new[] { "a.png", "b.png", "c.png" },
                frames.Select(f => f.TextureName));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Copy_MixedChainAndFrame_ClearsAnimationClipboardAndShowsError()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            RebuildTree(window);

            await window.Clipboard!.SetTextAsync(
                ClipboardPayload.Serialize(new List<AnimationFrameSave> { frame }));

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = FirstChainNode(tree);
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(chainNode);
            tree.SelectedItems.Add(chainNode.Children[0]);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            var clip = await window.Clipboard!.TryGetTextAsync();
            Assert.True(string.IsNullOrWhiteSpace(clip));

            var banner = window.Notifications.ErrorBanner;
            var bannerText = window.Notifications.ErrorBannerText;
            Assert.True(banner.IsVisible);
            Assert.Equal(SelectionCopyContext.MixedSelectionMessage, bannerText.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Paste_ThreeFrames_SelectsAllPastedInTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var seed = new AnimationFrameSave { TextureName = "seed.png", FrameLength = 0.1f };
            chain.Frames.Add(seed);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            RebuildTree(window);

            var clipFrames = new List<AnimationFrameSave>
            {
                new() { TextureName = "a.png", FrameLength = 0.1f },
                new() { TextureName = "b.png", FrameLength = 0.1f },
                new() { TextureName = "c.png", FrameLength = 0.1f },
            };
            await window.Clipboard!.SetTextAsync(ClipboardPayload.Serialize(clipFrames));

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var seedNode = FirstChainNode(tree).Children[0];
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(seedNode);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            Assert.Equal(4, chain.Frames.Count);
            var pasted = chain.Frames.Skip(1).ToList();
            Assert.Equal(new[] { "a.png", "b.png", "c.png" }, pasted.Select(f => f.TextureName));

            Assert.Equal(3, ctx.SelectedState.SelectedFrames.Count);
            var selectedVms = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Equal(3, selectedVms.Count);
            var selectedFrames = selectedVms.Select(n => n.Data).Cast<AnimationFrameSave>().ToList();
            Assert.Equal(new[] { "a.png", "b.png", "c.png" }, selectedFrames.Select(f => f.TextureName));
            Assert.All(selectedFrames, f => Assert.Contains(f, pasted));
        }
        finally { window.Close(); }
    }

    // #937: pasted frames must preserve ShapesSave presence like every other frame-creation path
    // now does -- a shapeless source frame must not gain an empty ShapesSave through paste, or
    // the pasted copy bakes an empty shapesSave/ShapeCollectionSave block on the next save.
    [AvaloniaFact]
    public async Task Paste_Frame_WithNoShapes_KeepsShapesSaveNull()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var seed = new AnimationFrameSave { TextureName = "seed.png", FrameLength = 0.1f };
            chain.Frames.Add(seed);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            RebuildTree(window);

            var clipFrames = new List<AnimationFrameSave>
            {
                new() { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = null },
            };
            await window.Clipboard!.SetTextAsync(ClipboardPayload.Serialize(clipFrames));

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var seedNode = FirstChainNode(tree).Children[0];
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(seedNode);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            var pasted = chain.Frames.Single(f => f.TextureName == "a.png");
            Assert.Null(pasted.ShapesSave);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Paste_TwoChains_SelectsAllPastedInTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var walk = new AnimationChainSave { Name = "Walk" };
            walk.Frames.Add(new AnimationFrameSave { TextureName = "w.png", FrameLength = 0.1f });
            var run = new AnimationChainSave { Name = "Run" };
            run.Frames.Add(new AnimationFrameSave { TextureName = "r.png", FrameLength = 0.1f });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.AddRange(new[] { walk, run });
            RebuildTree(window);

            var clipChains = new List<AnimationChainSave>
            {
                new() { Name = "Idle", Frames = { new AnimationFrameSave { TextureName = "i.png", FrameLength = 0.1f } } },
                new() { Name = "Jump", Frames = { new AnimationFrameSave { TextureName = "j.png", FrameLength = 0.1f } } },
            };
            await window.Clipboard!.SetTextAsync(ClipboardPayload.Serialize(clipChains));

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var runNode = tree.ItemsSource!.Cast<TreeNodeVm>().First(n => n.Header == "Run");
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(runNode);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            Assert.Equal(4, ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count);
            var pastedChains = ctx.SelectedState.SelectedChains;
            Assert.Equal(2, pastedChains.Count);
            Assert.Equal(new[] { "Idle", "Jump" }, pastedChains.Select(c => c.Name));

            var selectedVms = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Equal(2, selectedVms.Count);
            Assert.Equal(new[] { "Idle", "Jump" },
                selectedVms.Select(n => n.Data).Cast<AnimationChainSave>().Select(c => c.Name));
        }
        finally { window.Close(); }
    }

    // #1099: copying a shape then multi-selecting frames and pasting must paste into every
    // selected frame, not just one.
    [AvaloniaFact]
    public async Task Paste_ShapeIntoMultipleSelectedFrames_PastesIntoEveryFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var sourceFrame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
            sourceFrame.ShapesSave = new ShapesSave();
            sourceFrame.ShapesSave.Shapes.Add(new AARectSave { Name = "Hit" });
            var targetFrame1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f };
            var targetFrame2 = new AnimationFrameSave { TextureName = "c.png", FrameLength = 0.1f };
            chain.Frames.AddRange(new[] { sourceFrame, targetFrame1, targetFrame2 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var frameNodes = FirstChainNode(tree).Children;
            var shapeNode = frameNodes[0].Children[0];

            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(shapeNode);
            FlushUi();
            tree.Focus();
            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(frameNodes[1]);
            tree.SelectedItems.Add(frameNodes[2]);
            FlushUi();
            tree.Focus();
            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            Assert.Single(targetFrame1.ShapesSave!.Shapes);
            Assert.Single(targetFrame2.ShapesSave!.Shapes);
            Assert.NotSame(targetFrame1.ShapesSave.Shapes[0], targetFrame2.ShapesSave.Shapes[0]);
            Assert.IsType<AARectSave>(targetFrame1.ShapesSave.Shapes[0]);
            Assert.IsType<AARectSave>(targetFrame2.ShapesSave.Shapes[0]);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CtrlD_ThreeFrames_SelectsAllDuplicatesInTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
            var f1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f };
            var f2 = new AnimationFrameSave { TextureName = "c.png", FrameLength = 0.1f };
            chain.Frames.AddRange(new[] { f0, f1, f2 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var frameNodes = FirstChainNode(tree).Children;
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(frameNodes[0]);
            tree.SelectedItems.Add(frameNodes[1]);
            tree.SelectedItems.Add(frameNodes[2]);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            Assert.Equal(6, chain.Frames.Count);
            var copies = chain.Frames.Skip(3).ToList();
            Assert.Equal(new[] { "a.png", "b.png", "c.png" }, copies.Select(f => f.TextureName));

            Assert.Equal(3, ctx.SelectedState.SelectedFrames.Count);
            var selectedVms = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Equal(3, selectedVms.Count);
            var selectedFrames = selectedVms.Select(n => n.Data).Cast<AnimationFrameSave>().ToList();
            Assert.All(selectedFrames, f => Assert.Contains(f, copies));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CtrlD_TwoChains_SelectsAllDuplicatesInTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var walk = new AnimationChainSave { Name = "Walk" };
            walk.Frames.Add(new AnimationFrameSave { TextureName = "w.png", FrameLength = 0.1f });
            var run = new AnimationChainSave { Name = "Run" };
            run.Frames.Add(new AnimationFrameSave { TextureName = "r.png", FrameLength = 0.1f });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.AddRange(new[] { walk, run });
            RebuildTree(window);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var roots = tree.ItemsSource!.Cast<TreeNodeVm>().ToList();
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(roots[0]);
            tree.SelectedItems.Add(roots[1]);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            Assert.Equal(4, ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count);
            var copies = ctx.SelectedState.SelectedChains;
            Assert.Equal(2, copies.Count);
            Assert.All(copies, c => c.Name.EndsWith("Copy"));

            var selectedVms = tree.SelectedItems!.Cast<TreeNodeVm>().ToList();
            Assert.Equal(2, selectedVms.Count);
            Assert.Equal(copies, selectedVms.Select(n => n.Data).Cast<AnimationChainSave>().ToList());
        }
        finally { window.Close(); }
    }
}
