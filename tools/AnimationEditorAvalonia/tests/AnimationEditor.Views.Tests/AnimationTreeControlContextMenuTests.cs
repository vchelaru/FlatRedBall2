using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// Phase 2 of #754: the browser's right-click tree menu, built from the same
/// <see cref="TreeMenuPlanBuilder"/> plan desktop's MainWindow consumes (see
/// AnimationEditor.App.Tests.TreeContextMenuTests for the desktop equivalents these mirror) and
/// wired here via <see cref="AnimationTreeControl.EnableContextMenu"/>.
/// </summary>
public class AnimationTreeControlContextMenuTests
{
    private sealed class FakeProjectManager : IProjectManager
    {
        public AnimationChainListSave? AnimationChainListSave { get; set; }
        public TileMapInformationList TileMapInformationList { get; set; } = new();
        public FilePath[] ReferencedPngs => Array.Empty<FilePath>();
        public string? FileName { get; set; }
        public TextureCoordinateType OnDiskCoordinateType { get; set; }

        public void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null) { }

        public void SaveAnimationChainList(string targetPath) { }
        public void SaveAnimationChainList(System.IO.Stream stream) { }
        public string? ResolveFilesPanelRoot() => null;
        public (int Width, int Height)? GetTextureSizeInPixels(string textureName) => null;

        public IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory) =>
            Array.Empty<string>();
    }

    private sealed class Harness
    {
        public required Window Window;
        public required AnimationTreeControl Control;
        public required ISelectedState SelectedState;
        public required IAppCommands AppCommands;
        public required AnimationChainListSave Acls;
        public required IPendingCutState PendingCutState;
    }

    private static Harness Build(AnimationChainListSave? acls = null)
    {
        acls ??= new AnimationChainListSave();
        var pm = new FakeProjectManager { AnimationChainListSave = acls };
        var selectedState = new SelectedState(pm);
        var events = new ApplicationEvents();
        var appState = new AppState(events, selectedState);
        var ioManager = new IoManager(appState);
        var objectFinder = new ObjectFinder(pm);
        var undoManager = new UndoManager();
        var appCommands = new AppCommands(pm, selectedState, events, ioManager, objectFinder, undoManager);
        var pendingCutState = new PendingCutState();

        var control = new AnimationTreeControl();
        control.InitializeServices(selectedState, acls);
        control.EnableRename(appCommands);
        control.EnableContextMenu(appCommands, objectFinder, pm, pendingCutState);
        events.AnimationChainsChanged += control.Refresh;

        var window = new Window { Content = control, Width = 400, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Harness
        {
            Window = window,
            Control = control,
            SelectedState = selectedState,
            AppCommands = appCommands,
            Acls = acls,
            PendingCutState = pendingCutState,
        };
    }

    private static List<TreeNodeVm> Roots(Harness h) =>
        ((System.Collections.IEnumerable)h.Control.TreeView.ItemsSource!).Cast<TreeNodeVm>().ToList();

    // Selects the node and rebuilds the context menu by invoking the real Opening handler
    // directly (the popup itself doesn't open under the headless backend), mirroring
    // AnimationEditor.App.Tests.TreeContextMenuTests.OpenMenuFor.
    private static List<object> OpenMenuFor(Harness h, TreeNodeVm node)
    {
        h.Control.TreeView.SelectedItem = node;
        Dispatcher.UIThread.RunJobs();

        typeof(AnimationTreeControl)
            .GetMethod("OnTreeContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(h.Control, new object?[] { null, new CancelEventArgs() });
        return h.Control.TreeView.ContextMenu!.Items.Cast<object>().ToList();
    }

    private static int IndexOfItem(List<object> items, string header) =>
        items.FindIndex(o => o is MenuItem m && (string?)m.Header == header);

    private static void ClickMenuItem(Harness h, string header)
    {
        var item = h.Control.TreeView.ContextMenu!.Items
            .OfType<MenuItem>()
            .First(m => m.Header?.ToString() == header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void ClickSubMenuItem(Harness h, string parentHeader, string childHeader)
    {
        var parent = h.Control.TreeView.ContextMenu!.Items
            .OfType<MenuItem>()
            .First(m => m.Header?.ToString() == parentHeader);
        var child = parent.Items.OfType<MenuItem>().First(m => m.Header?.ToString() == childHeader);
        child.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    // ── Menu shape ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void ChainMenu_DuplicateIsSubmenuWithThreeVariants()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var items = OpenMenuFor(h, Roots(h)[0]);
            var duplicate = items.OfType<MenuItem>().Single(m => (string?)m.Header == "Duplicate");
            var children = duplicate.Items.OfType<MenuItem>().Select(m => (string?)m.Header).ToList();
            Assert.Equal(new[] { "Original", "Flip Horizontal", "Flip Vertical" }, children);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void ChainMenu_CopyPasteDuplicateAreConsecutive()
    {
        var chain = new AnimationChainSave { Name = "Run" };
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var items = OpenMenuFor(h, Roots(h)[0]);
            int copy = IndexOfItem(items, "Copy");
            Assert.True(copy >= 0);
            Assert.Equal(copy + 1, IndexOfItem(items, "Cut"));
            Assert.Equal(copy + 2, IndexOfItem(items, "Paste"));
            Assert.Equal(copy + 3, IndexOfItem(items, "Duplicate"));
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void RectMenu_HasCopyPasteDuplicateRenameAndMatchFrameSize()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "run.png", ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "Rect" };
        var circle = new CircleSave { Name = "Circle" };
        frame.ShapesSave.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(circle);
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var rectNode = Roots(h)[0].Children[0].Children[0];
            var items = OpenMenuFor(h, rectNode);

            int copy = IndexOfItem(items, "Copy");
            Assert.True(copy >= 0);
            Assert.Equal(copy + 1, IndexOfItem(items, "Cut"));
            Assert.Equal(copy + 2, IndexOfItem(items, "Paste"));
            Assert.Equal(copy + 3, IndexOfItem(items, "Duplicate"));
            Assert.True(IndexOfItem(items, "Rename…") >= 0);
            Assert.True(IndexOfItem(items, "Match Frame Size") >= 0);
            Assert.True(IndexOfItem(items, "Delete Rectangle") >= 0);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void CircleMenu_HasCopyPasteDuplicateRename_AndNoMatchFrameSize()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "run.png", ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "Rect" };
        var circle = new CircleSave { Name = "Circle" };
        frame.ShapesSave.Shapes.Add(rect);
        frame.ShapesSave.Shapes.Add(circle);
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var circleNode = Roots(h)[0].Children[0].Children[1];
            var items = OpenMenuFor(h, circleNode);

            Assert.True(IndexOfItem(items, "Rename…") >= 0);
            Assert.True(IndexOfItem(items, "Delete Circle") >= 0);
            Assert.Equal(-1, IndexOfItem(items, "Match Frame Size"));
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void FrameMenu_ShowsCopyTexturePath_NotViewTextureInExplorer()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "run.png" };
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var frameNode = Roots(h)[0].Children[0];
            var items = OpenMenuFor(h, frameNode);

            Assert.True(IndexOfItem(items, "Copy Texture Path") >= 0);
            Assert.Equal(-1, IndexOfItem(items, "View Texture in Explorer"));
        }
        finally { h.Window.Close(); }
    }

    // ── Behavior ──────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void DeleteFrame_ContextMenu_DeletesFrame()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "a.png" };
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var frameNode = Roots(h)[0].Children[0];
            OpenMenuFor(h, frameNode);
            ClickMenuItem(h, "Delete Frame");

            Assert.Empty(chain.Frames);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void DeleteRectangle_ContextMenu_DeletesRectangle()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "R0" };
        frame.ShapesSave.Shapes.Add(rect);
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var rectNode = Roots(h)[0].Children[0].Children[0];
            OpenMenuFor(h, rectNode);
            ClickMenuItem(h, "Delete Rectangle");

            Assert.Empty(frame.ShapesSave.AARectSaves);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void DuplicateChain_ContextMenu_Original_DuplicatesChain()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            OpenMenuFor(h, Roots(h)[0]);
            ClickSubMenuItem(h, "Duplicate", "Original");

            Assert.Equal(2, acls.AnimationChains.Count);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void DuplicateChain_ContextMenu_FlipHorizontal_DuplicatesChainFlipped()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FlipHorizontal = false });
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            OpenMenuFor(h, Roots(h)[0]);
            ClickSubMenuItem(h, "Duplicate", "Flip Horizontal");

            Assert.Equal(2, acls.AnimationChains.Count);
            var copy = acls.AnimationChains.Single(c => c != chain);
            Assert.True(copy.Frames[0].FlipHorizontal);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public void RenameMenuItem_Rectangle_BeginsInlineEditWithCurrentName()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "a.png", ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "Hitbox" };
        frame.ShapesSave.Shapes.Add(rect);
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var rectNode = Roots(h)[0].Children[0].Children[0];
            OpenMenuFor(h, rectNode);
            ClickMenuItem(h, "Rename…");

            Assert.True(rectNode.IsEditing);
            Assert.Equal("Hitbox", rectNode.EditingText);

            h.Control.CommitRename(rectNode, "Renamed");
            Assert.Equal("Renamed", rect.Name);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task CopyTexturePath_ContextMenu_WritesTextureNameToClipboard()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "sprites/run.png" };
        chain.Frames.Add(frame);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            var frameNode = Roots(h)[0].Children[0];
            OpenMenuFor(h, frameNode);
            ClickMenuItem(h, "Copy Texture Path");
            Dispatcher.UIThread.RunJobs();

            var text = await h.Window.Clipboard!.TryGetTextAsync();
            Assert.Equal("sprites/run.png", text);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task CopyThenPasteChain_ContextMenu_AddsCopyOfChain()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        var h = Build(acls);
        try
        {
            OpenMenuFor(h, Roots(h)[0]);
            ClickMenuItem(h, "Copy");
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();

            OpenMenuFor(h, Roots(h)[0]);
            ClickMenuItem(h, "Paste");
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, acls.AnimationChains.Count);
        }
        finally { h.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task CutThenPasteFrame_ContextMenu_MovesFrameToNewChain()
    {
        var source = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "a.png" };
        source.Frames.Add(frame);
        var target = new AnimationChainSave { Name = "Run" };
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(source);
        acls.AnimationChains.Add(target);
        var h = Build(acls);
        try
        {
            var roots = Roots(h);
            var frameNode = roots[0].Children[0];
            OpenMenuFor(h, frameNode);
            ClickMenuItem(h, "Cut");
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();

            OpenMenuFor(h, roots[1]); // "Run" chain node -- paste target
            ClickMenuItem(h, "Paste");
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(source.Frames);
            Assert.Single(target.Frames);
            Assert.Equal("a.png", target.Frames[0].TextureName);
        }
        finally { h.Window.Close(); }
    }

    // ── Right-click selection ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void RightClick_DifferentNode_SelectsItBeforeMenuBuilds()
    {
        var walk = new AnimationChainSave { Name = "Walk" };
        var run = new AnimationChainSave { Name = "Run" };
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(walk);
        acls.AnimationChains.Add(run);
        var h = Build(acls);
        try
        {
            var roots = Roots(h);
            h.Control.TreeView.SelectedItem = roots[0];
            Dispatcher.UIThread.RunJobs();

            var tvi = h.Control.TreeView.GetVisualDescendants().OfType<TreeViewItem>()
                .First(t => ReferenceEquals(t.DataContext, roots[1]));
            var centre = new Point(tvi.Bounds.Width / 2, tvi.Bounds.Height / 2);
            var pointInWindow = tvi.TranslatePoint(centre, h.Window)!.Value;
            h.Window.MouseDown(pointInWindow, MouseButton.Right);
            Dispatcher.UIThread.RunJobs();

            Assert.Same(roots[1], h.Control.TreeView.SelectedItem);
        }
        finally { h.Window.Close(); }
    }
}
