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

    // Opens a project folder holding hero.achx and opens that file as a tab, so the tab is
    // "in the project" the way a file clicked from the Project panel is.
    private static async Task<string> OpenProjectFolderWithOpenTabAsync(MainWindow window, string dir)
    {
        var heroPath = Path.Combine(dir, "hero.achx");
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        list.Save(heroPath);

        await window.OpenProjectFolderForTestAsync(dir);
        await window.OpenFileAsTab(heroPath);
        Dispatcher.UIThread.RunJobs();
        return heroPath;
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [AvaloniaFact]
    public async Task MenuCloseProject_Click_ClearsTreeTabsAndProjectState()
    {
        var (window, ctx) = CreateWindow();
        var dir = NewTempDir();
        try
        {
            await OpenProjectFolderWithOpenTabAsync(window, dir);
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
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    private static bool IsCloseProjectEnabledAfterOpeningFileMenu(MainWindow window)
    {
        window.FindControl<MenuItem>("MenuFile")!
              .RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
        Dispatcher.UIThread.RunJobs();
        return window.FindControl<MenuItem>("MenuCloseProject")!.IsEnabled;
    }

    // Close Project Folder does nothing without an open folder, so the menu item is disabled then.
    [AvaloniaFact]
    public async Task MenuCloseProject_EnabledOnlyWhileProjectFolderIsOpen()
    {
        var (window, ctx) = CreateWindow();
        var dir = NewTempDir();
        try
        {
            Assert.False(IsCloseProjectEnabledAfterOpeningFileMenu(window));

            await window.OpenProjectFolderForTestAsync(dir);
            Assert.True(IsCloseProjectEnabledAfterOpeningFileMenu(window));

            window.CloseProjectFolder();
            Assert.False(IsCloseProjectEnabledAfterOpeningFileMenu(window));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task NativeMenuCloseProjectFolder_ClearsTabsAndProjectState()
    {
        var (window, ctx) = CreateWindow();
        var dir = NewTempDir();
        try
        {
            await OpenProjectFolderWithOpenTabAsync(window, dir);
            var tabManager = GetTabManager(window);
            Assert.NotEmpty(tabManager.Tabs);

            window.CreateNativeMenuActions().CloseProjectFolder();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(tabManager.Tabs);
            Assert.Empty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task CloseProjectFolder_ProjectFolderOpen_ClearsProjectPanelTree()
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

            window.CloseProjectFolder();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(window.ProjectPanel.TreeRoots);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // Issue #1360: Untitled tabs are not part of any project folder, so closing the project
    // neither closes them nor asks about discarding them.
    [AvaloniaFact]
    public void CloseProjectFolder_UntitledTabWithContent_SurvivesWithoutPrompt()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // File > New opens a genuine, active Untitled tab; give it content.
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            var tabManager = GetTabManager(window);
            Assert.NotEmpty(tabManager.Tabs);

            int prompts = 0;
            ctx.AppCommands.ConfirmAsync = (_, _) => { prompts++; return Task.FromResult(false); };

            window.CloseProjectFolder();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, prompts);
            Assert.NotEmpty(tabManager.Tabs);
            Assert.NotEmpty(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }
}
