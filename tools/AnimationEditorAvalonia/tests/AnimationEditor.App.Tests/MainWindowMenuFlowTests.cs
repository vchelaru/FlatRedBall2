using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// End-to-end headless MainWindow integration tests covering every menu-level
/// user flow: File→New/Load/Save/SaveAs, Edit→Undo/Redo, and context-menu
/// Add Frame / Add Shape actions.
/// </summary>
public class MainWindowMenuFlowTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    private static TreeView GetTree(MainWindow w)
        => w.FindControl<TreeView>("AnimTree")
           ?? throw new InvalidOperationException("AnimTree not found");

    private static ObservableCollection<TreeNodeVm> GetRoots(TreeView tree)
        => (ObservableCollection<TreeNodeVm>)(tree.ItemsSource
           ?? throw new InvalidOperationException("AnimTree has no ItemsSource"));

    private static void TriggerRefreshTreeView(MainWindow window)
        => typeof(MainWindow)
            .GetMethod("RefreshTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);

    private static void TriggerContextMenuOpening(MainWindow window)
        => typeof(MainWindow)
            .GetMethod("OnTreeContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [null, new CancelEventArgs()]);

    private static void ClickContextMenuItem(MainWindow window, string header)
    {
        var item = GetTree(window).ContextMenu!.Items
            .OfType<MenuItem>()
            .First(m => m.Header?.ToString() == header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    /// <summary>
    /// Writes an .achx file to <paramref name="dir"/> containing one chain per name.
    /// Returns the full file path.
    /// </summary>
    private static string WriteAchx(string dir, params string[] chainNames)
    {
        var path = Path.Combine(dir, "test.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (var name in chainNames)
            acls.AnimationChains.Add(new AnimationChainSave { Name = name });
        acls.Save(path);
        return path;
    }

    // ── File → Load ───────────────────────────────────────────────────────────

    /// <summary>
    /// Regression test for the gap identified in PR #176: loading an .achx file
    /// must populate the tree view with one root node per chain.
    /// </summary>
    [AvaloniaFact]
    public void Load_PopulatesTree()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, _) = CreateWindow();
        try
        {
            var path = WriteAchx(dir, "Walk", "Run");

            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path]);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, GetRoots(GetTree(window)).Count);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // ── File → New ────────────────────────────────────────────────────────────

    /// <summary>
    /// File → New must clear the tree immediately (regression: OnNewClick was missing RefreshTreeView).
    /// </summary>
    [AvaloniaFact]
    public void New_ClearsTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(
                new AnimationChainSave { Name = "Walk" });
            TriggerRefreshTreeView(window);
            Assert.Single(GetRoots(GetTree(window)));

            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Empty(GetRoots(GetTree(window)));
        }
        finally { window.Close(); }
    }

    // ── File → Save ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Save_WithFileName_WritesFileToDisk()
    {
        var dir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "out.achx");
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(
                new AnimationChainSave { Name = "Idle" });
            ctx.ProjectManager.FileName = path;

            window.FindControl<MenuItem>("MenuSave")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.True(File.Exists(path));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // ── File → Save As ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SaveAs_WithStubDialog_CreatesFileAndUpdatesFileName()
    {
        var dir  = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "out.achx");
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.FileDialogService = new MenuFlowStubFileDialogService(path);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(
                new AnimationChainSave { Name = "Idle" });

            window.FindControl<MenuItem>("MenuSaveAs")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.True(File.Exists(path));
            Assert.Equal(path, ctx.ProjectManager.FileName);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // ── Edit → Undo ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Undo_AfterAddChain_RemovesChainFromTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();
            Assert.Single(GetRoots(GetTree(window)));

            window.FindControl<MenuItem>("MenuUndo")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(GetRoots(GetTree(window)));
        }
        finally { window.Close(); }
    }

    // ── Edit → Redo ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Redo_AfterUndoAddChain_RestoresChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();
            window.FindControl<MenuItem>("MenuUndo")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(GetRoots(GetTree(window)));

            window.FindControl<MenuItem>("MenuRedo")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Single(GetRoots(GetTree(window)));
        }
        finally { window.Close(); }
    }

    // ── Context menu: Add Frame ───────────────────────────────────────────────

    [AvaloniaFact]
    public void ContextMenu_AddFrame_AddsFrameToChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            var chainVm = new TreeNodeVm { Header = "Walk", Data = chain };
            GetRoots(GetTree(window)).Add(chainVm);
            GetTree(window).SelectedItem = chainVm;

            TriggerContextMenuOpening(window);
            ClickContextMenuItem(window, "Add Frame");

            Assert.Single(chain.Frames);
        }
        finally { window.Close(); }
    }

    // ── Context menu: Add AxisAlignedRectangle ────────────────────────────────

    [AvaloniaFact]
    public void ContextMenu_AddRect_AddsRectToFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            var frameVm = new TreeNodeVm { Header = "Frame 0", Data = frame };
            GetRoots(GetTree(window)).Add(frameVm);
            GetTree(window).SelectedItem = frameVm;

            TriggerContextMenuOpening(window);
            ClickContextMenuItem(window, "Add AxisAlignedRectangle");

            Assert.Single(frame.ShapesSave!.AARectSaves);
        }
        finally { window.Close(); }
    }

    // ── Context menu: Add Circle ──────────────────────────────────────────────

    [AvaloniaFact]
    public void ContextMenu_AddCircle_AddsCircleToFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            var frameVm = new TreeNodeVm { Header = "Frame 0", Data = frame };
            GetRoots(GetTree(window)).Add(frameVm);
            GetTree(window).SelectedItem = frameVm;

            TriggerContextMenuOpening(window);
            ClickContextMenuItem(window, "Add Circle");

            Assert.Single(frame.ShapesSave!.CircleSaves);
        }
        finally { window.Close(); }
    }

    // ── Expand + select after adding shape (issue #273) ───────────────────────

    [AvaloniaFact]
    public void ContextMenu_AddRect_ExpandsFrameNodeAndSelectsShape()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var chainVm = new TreeNodeVm { Header = "Walk", Data = chain, IsChainNode = true, IsExpanded = true };
            var frameVm = new TreeNodeVm { Header = "Frame 1", Data = frame, IsFrameNode = true };
            chainVm.Children.Add(frameVm);
            GetRoots(GetTree(window)).Add(chainVm);
            GetTree(window).SelectedItem = frameVm;
            ctx.SelectedState.SelectedFrame = frame;

            TriggerContextMenuOpening(window);
            ClickContextMenuItem(window, "Add AxisAlignedRectangle");
            Dispatcher.UIThread.RunJobs();

            Assert.True(frameVm.IsExpanded, "frame node should be expanded after adding a rectangle");
            var selectedNode = GetTree(window).SelectedItem as TreeNodeVm;
            Assert.NotNull(selectedNode);
            Assert.IsType<AARectSave>(selectedNode.Data);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ContextMenu_AddCircle_ExpandsFrameNodeAndSelectsShape()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            var chainVm = new TreeNodeVm { Header = "Walk", Data = chain, IsChainNode = true, IsExpanded = true };
            var frameVm = new TreeNodeVm { Header = "Frame 1", Data = frame, IsFrameNode = true };
            chainVm.Children.Add(frameVm);
            GetRoots(GetTree(window)).Add(chainVm);
            GetTree(window).SelectedItem = frameVm;
            ctx.SelectedState.SelectedFrame = frame;

            TriggerContextMenuOpening(window);
            ClickContextMenuItem(window, "Add Circle");
            Dispatcher.UIThread.RunJobs();

            Assert.True(frameVm.IsExpanded, "frame node should be expanded after adding a circle");
            var selectedNode = GetTree(window).SelectedItem as TreeNodeVm;
            Assert.NotNull(selectedNode);
            Assert.IsType<CircleSave>(selectedNode.Data);
        }
        finally { window.Close(); }
    }
}

/// <summary>Test double that returns a pre-configured path (or null) from every dialog.</summary>
internal sealed class MenuFlowStubFileDialogService : IFileDialogService
{
    private readonly string? _path;

    public MenuFlowStubFileDialogService(string? path) => _path = path;

    public Task<string?> PickSaveFileAsync(string title, string defaultExtension, string fileTypeDescription)
        => Task.FromResult(_path);

    public Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription)
        => Task.FromResult(_path);
}
