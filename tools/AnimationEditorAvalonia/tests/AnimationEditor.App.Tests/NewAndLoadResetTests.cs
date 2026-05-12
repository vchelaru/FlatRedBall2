using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that File → New and File → Load reset the sprite shown in the
/// wireframe and the animation playing in the preview by clearing selection.
/// </summary>
public class NewAndLoadResetTests
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

    private static string WriteAchx(string dir, params string[] chainNames)
    {
        var path = Path.Combine(dir, "test.achx");
        var acls = new AnimationChainListSave();
        foreach (var name in chainNames)
            acls.AnimationChains.Add(new AnimationChainSave { Name = name });
        acls.Save(path);
        return path;
    }

    // ── File → New ────────────────────────────────────────────────────────────

    /// <summary>
    /// File → New must clear the tree so it shows no chains.
    /// (Regression: OnNewClick was missing RefreshTreeView.)
    /// </summary>
    [AvaloniaFact]
    public void New_ClearsTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            var tree  = window.FindControl<TreeView>("AnimTree")!;
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)tree.ItemsSource!;
            Assert.Empty(roots);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// File → New must reset SelectedChain and SelectedFrame so the wireframe
    /// and preview stop showing the previous file's sprite and animation.
    /// </summary>
    [AvaloniaFact]
    public void New_ClearsSelection()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame; // simulate a frame being selected

            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Null(ctx.SelectedState.SelectedChain);
            Assert.Null(ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// File → New must reset SelectedChain even when selection was made via the tree.
    /// In a real window Avalonia fires AnimTree.SelectionChanged when _treeRoots is
    /// cleared; the handler must not re-apply the stale tree item after Reset().
    /// </summary>
    [AvaloniaFact]
    public void New_ClearsSelection_WhenSelectedViaTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            var tree  = window.FindControl<TreeView>("AnimTree")!;
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)tree.ItemsSource!;
            Assert.NotEmpty(roots);

            // Simulate the user clicking the chain node in the tree
            tree.SelectedItem = roots[0];
            Dispatcher.UIThread.RunJobs();

            // File → New
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Null(ctx.SelectedState.SelectedChain);
            Assert.Null(ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }

    // ── File → Load ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loading a new .achx must clear the old selection so the wireframe and
    /// preview stop rendering the previous file's sprite/animation.
    /// </summary>
    [AvaloniaFact]
    public void Load_ClearsSelectionBeforePopulatingNewFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            // Set up an initial chain and select it
            var oldChain = new AnimationChainSave { Name = "OldWalk" };
            var oldFrame = new AnimationFrameSave { TextureName = "Old.png", ShapesSave = new ShapesSave() };
            oldChain.Frames.Add(oldFrame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(oldChain);
            ctx.SelectedState.SelectedFrame = oldFrame;

            // Load a different .achx
            var path = WriteAchx(dir, "NewRun");
            typeof(MainWindow)
                .GetMethod("LoadAnimationFile", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path]);
            Dispatcher.UIThread.RunJobs();

            Assert.Null(ctx.SelectedState.SelectedFrame);
            // SelectedChain should be the first chain of the *new* file, not the old one
            Assert.Equal("NewRun", ctx.SelectedState.SelectedChain?.Name);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
