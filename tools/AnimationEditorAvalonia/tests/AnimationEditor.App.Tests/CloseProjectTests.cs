using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #948: File → Close Project must reset the editor to the same blank state as a fresh
/// app launch -- ProjectManager, SelectedState, undo stack, tabs, and the tree UI.
/// </summary>
public class CloseProjectTests
{
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

    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    [AvaloniaFact]
    public void MenuCloseProject_Click_ClearsTreeTabsAndProjectState()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // Add a chain to the currently-open (untitled) document and register it as a tab,
            // matching how a real "New" document with content becomes a tab.
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            typeof(MainWindow)
                .GetMethod("EnsureCurrentEditorContentHasTab", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, null);
            Dispatcher.UIThread.RunJobs();

            var tabManager = GetTabManager(window);
            Assert.NotEmpty(tabManager.Tabs);

            window.FindControl<MenuItem>("MenuCloseProject")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(tabManager.Tabs);
            Assert.Null(ctx.ProjectManager.FileName);
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);

            var tree  = window.FindControl<TreeView>("AnimTree")!;
            var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
            Assert.Empty(roots);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CloseProjectAsync_ProjectFolderOpen_ClearsProjectPanelTree()
    {
        var (window, ctx) = CreateWindow();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            new AnimationChainListSave().Save(Path.Combine(dir, "hero.achx"));

            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(window.ProjectPanel.TreeRoots);

            await window.CloseProjectAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(window.ProjectPanel.TreeRoots);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task CloseProjectAsync_UnsavedUntitledTabDeclined_LeavesProjectUntouched()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // File > New opens a genuine, active Untitled tab (unlike EnsureCurrentEditorContentHasTab,
            // which only registers a background tab); give it content so it is at risk of being discarded.
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            var tabManager = GetTabManager(window);
            Assert.NotEmpty(tabManager.Tabs);

            ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(false);

            await window.CloseProjectAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.NotEmpty(tabManager.Tabs);
            Assert.NotEmpty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }
}
