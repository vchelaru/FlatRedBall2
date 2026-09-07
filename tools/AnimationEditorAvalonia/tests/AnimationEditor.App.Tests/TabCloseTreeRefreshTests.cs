using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1038: closing an unsaved tab left the tree view showing the closed tab's stale
/// content instead of refreshing to the reactivated tab. Root cause: <c>EditorProjectModelChanged</c>
/// is handled via <c>Dispatcher.UIThread.InvokeAsync</c>, so <c>SyncTabCacheFromEditor(path)</c> can
/// run after the live document has already moved on to a different tab (e.g. two documents loaded
/// back-to-back at startup: a settings-restored tab, then a crash-recovered one). It then captured
/// whatever was *currently* live onto the tab matching the stale <c>path</c>, poisoning that tab's
/// cache with a different tab's content -- served back verbatim on the next reactivation.
/// </summary>
public class TabCloseTreeRefreshTests
{
    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private static async Task CloseTabAsync(MainWindow window, TabEntry tab) =>
        await (Task)typeof(MainWindow)
            .GetMethod("CloseTabAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [tab])!;

    private static async Task ActivateTabAsync(MainWindow window, TabEntry tab) =>
        await (Task)typeof(MainWindow)
            .GetMethod("ActivateTabAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [tab])!;

    private static TreeView GetTree(MainWindow w) =>
        w.FindControl<TreeView>("AnimTree")
        ?? throw new InvalidOperationException("AnimTree control not found");

    private static void WriteAchx(string path, string chainName)
    {
        var acls = new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave
        {
            CoordinateType = FlatRedBall2.AnimationEditorCommon.TextureCoordinateType.Pixel,
        };
        acls.AnimationChains.Add(new FlatRedBall2.AnimationEditorCommon.AnimationChainSave { Name = chainName });
        acls.Save(path);
    }

    [AvaloniaFact]
    public async Task ClosingUnsavedTab_ReactivatesSavedTab_RefreshesAnimTreeToItsContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var savedPath = Path.Combine(dir, "saved.achx");
        WriteAchx(savedPath, "Idle");

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        window.ShowSaveDiscardCancelDialogAsync = (_, _) => Task.FromResult(SaveDiscardCancelChoice.Discard);
        try
        {
            // Tab A: a saved, on-disk file with chain "Idle".
            await window.OpenFileAsTab(savedPath);
            Dispatcher.UIThread.RunJobs();

            // Tab B: an untitled tab with different content ("Walk"), becomes the active tab.
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            var tabManager = GetTabManager(window);
            var untitledTab = tabManager.ActiveTab!;
            Assert.NotEqual(new AnimationEditor.Core.Paths.FilePath(savedPath), untitledTab.Path);

            // Close the untitled ("Walk") tab -- tab A ("Idle") becomes active again.
            await CloseTabAsync(window, untitledTab);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(tabManager.Tabs);
            Assert.Equal(new AnimationEditor.Core.Paths.FilePath(savedPath), tabManager.ActiveTab!.Path);

            var tree = GetTree(window);
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)
                tree.ItemsSource!;

            Assert.Contains(roots, n => n.Header == "Idle");
            Assert.DoesNotContain(roots, n => n.Header == "Walk");
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ClosingUnsavedTab_ReactivatesSavedTab_ResyncsProjectPanelSelectionToIt()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var savedPath = Path.Combine(dir, "saved.achx");
        WriteAchx(savedPath, "Idle");

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        window.ShowSaveDiscardCancelDialogAsync = (_, _) => Task.FromResult(SaveDiscardCancelChoice.Discard);
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();
            await window.ProjectPanel.ThumbnailLoadTask;

            // Tab A: the saved file. (File > Open doesn't itself sync the Project panel's
            // selection -- only a tab activation/reactivation does; see SyncProjectPanelSelectionTo.)
            await window.OpenFileAsTab(savedPath);
            Dispatcher.UIThread.RunJobs();

            // Tab B: File > New -- untitled, has no Project-panel row, becomes active.
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            var tabManager = GetTabManager(window);
            var untitledTab = tabManager.ActiveTab!;

            // Close the untitled tab -- tab A (on disk) becomes active again.
            await CloseTabAsync(window, untitledTab);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(tabManager.Tabs);
            Assert.NotNull(window.ProjectPanel.SelectedEntry);
            Assert.Equal(new AnimationEditor.Core.Paths.FilePath(savedPath),
                new AnimationEditor.Core.Paths.FilePath(
                    ((AnimationEditor.App.Services.DiskEditorFile)window.ProjectPanel.SelectedEntry!.File).FullPath));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // ── The literal issue #1038 repro ───────────────────────────────────────────
    //
    // 1. A prior session had file A open and saved -- OpenTabPaths/ActiveTabPath restore it.
    // 2. That same prior session also had an untitled tab with unsaved edits ("Walk"), which
    //    left a crash-recovery file behind (autosave-on-edit writes it whenever FileName is
    //    null). Since #1020, a fresh launch restores BOTH: tab A from settings, then the
    //    recovered content as a second, active, untitled tab (HandleStartupAsync).
    // 3. Closing the recovered/unsaved tab must reactivate tab A with its tree fully refreshed.
    [AvaloniaFact]
    public async Task StartupRestoresSavedTabPlusRecoveredUnsavedTab_ClosingRecoveredTab_RefreshesTreeToSavedTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var savedPath = Path.Combine(dir, "saved.achx");
        WriteAchx(savedPath, "Idle");

        var ctx = TestHelpers.BuildServices();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new() { savedPath },
            ActiveTabPath = savedPath,
        }));

        var recovered = new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave
        {
            CoordinateType = FlatRedBall2.AnimationEditorCommon.TextureCoordinateType.Pixel,
        };
        recovered.AnimationChains.Add(new FlatRedBall2.AnimationEditorCommon.AnimationChainSave { Name = "Walk" });
        ctx.IoManager.WriteRecoveryFile(recovered);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.ShowSaveDiscardCancelDialogAsync = (_, _) => Task.FromResult(SaveDiscardCancelChoice.Discard);
        try
        {
            var tabManager = GetTabManager(window);

            // Sanity: the startup repro itself -- both tabs restored, the recovered one active.
            Assert.Equal(2, tabManager.Tabs.Count);
            var recoveredTab = tabManager.ActiveTab!;
            Assert.NotEqual(new AnimationEditor.Core.Paths.FilePath(savedPath), recoveredTab.Path);

            var tree = GetTree(window);
            var rootsAtStartup = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)
                tree.ItemsSource!;
            Assert.Contains(rootsAtStartup, n => n.Header == "Walk");

            // Close the recovered/unsaved tab -- the saved tab must become active with its own
            // (refreshed) tree content, not a leftover view of the closed tab's chains.
            await CloseTabAsync(window, recoveredTab);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(tabManager.Tabs);
            Assert.Equal(new AnimationEditor.Core.Paths.FilePath(savedPath), tabManager.ActiveTab!.Path);

            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)
                tree.ItemsSource!;
            Assert.Contains(roots, n => n.Header == "Idle");
            Assert.DoesNotContain(roots, n => n.Header == "Walk");
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // The user found the minimal repro doesn't even need a close: after the startup sequence
    // above poisons the saved tab's cache, simply clicking back to it (no closing at all) also
    // serves the poisoned "Walk" content instead of the saved tab's own "Idle" chain.
    [AvaloniaFact]
    public async Task StartupRestoresSavedTabPlusRecoveredUnsavedTab_ClickingSavedTab_ShowsItsOwnContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var savedPath = Path.Combine(dir, "saved.achx");
        WriteAchx(savedPath, "Idle");

        var ctx = TestHelpers.BuildServices();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new() { savedPath },
            ActiveTabPath = savedPath,
        }));

        var recovered = new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave
        {
            CoordinateType = FlatRedBall2.AnimationEditorCommon.TextureCoordinateType.Pixel,
        };
        recovered.AnimationChains.Add(new FlatRedBall2.AnimationEditorCommon.AnimationChainSave { Name = "Walk" });
        ctx.IoManager.WriteRecoveryFile(recovered);

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var tabManager = GetTabManager(window);
            Assert.Equal(2, tabManager.Tabs.Count);
            var savedTab = tabManager.Tabs.Single(t => t.Path == new AnimationEditor.Core.Paths.FilePath(savedPath));

            // Click the saved tab (no close involved) -- must show its own "Idle" content, not
            // whatever the recovered tab left poisoned in its cache.
            await ActivateTabAsync(window, savedTab);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new AnimationEditor.Core.Paths.FilePath(savedPath), tabManager.ActiveTab!.Path);

            var tree = GetTree(window);
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)
                tree.ItemsSource!;
            Assert.Contains(roots, n => n.Header == "Idle");
            Assert.DoesNotContain(roots, n => n.Header == "Walk");
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
