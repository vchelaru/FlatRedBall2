using System.IO;
using System.Linq;
using System.Reflection;
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
/// Issue #928: closing an untitled tab that has actual content must prompt to save it first,
/// separate from #927's "already-empty tab closes silently" case. "Has content" is any
/// document with at least one chain -- the same proxy <c>TabController.EnsureCurrentDocumentHasTab</c>
/// already uses to decide whether a fresh untitled document is worth a tab at all, since there
/// is no separate dirty flag (autosave-on-edit continuously writes the crash-recovery file
/// instead of tracking one).
/// </summary>
public class UntitledTabCloseSavePromptTests
{
    private sealed class StubFileDialogService(string? path) : IFileDialogService
    {
        public Task<string?> PickSaveFileAsync(string title, string defaultExtension, IReadOnlyList<FileTypeChoice> fileTypeChoices) =>
            Task.FromResult(path);

        public Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription) =>
            Task.FromResult<string?>(null);
    }

    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private static async Task CloseTabAsync(MainWindow window, TabEntry tab) =>
        await (Task)typeof(MainWindow)
            .GetMethod("CloseTabAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [tab])!;

    private static void FileNew(MainWindow window)
    {
        window.FindControl<MenuItem>("MenuNew")!
              .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task ClosingEmptyUntitledTab_DoesNotPrompt()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        bool promptShown = false;
        window.ShowSaveDiscardCancelDialogAsync = (_, _) =>
        {
            promptShown = true;
            return Task.FromResult(SaveDiscardCancelChoice.Cancel);
        };
        try
        {
            FileNew(window);
            var tab = GetTabManager(window).Tabs.Single();

            await CloseTabAsync(window, tab);
            Dispatcher.UIThread.RunJobs();

            Assert.False(promptShown);
            Assert.Empty(GetTabManager(window).Tabs);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ClosingUntitledTabWithContent_Cancel_LeavesTabOpen()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        window.ShowSaveDiscardCancelDialogAsync = (_, _) => Task.FromResult(SaveDiscardCancelChoice.Cancel);
        try
        {
            FileNew(window);
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();
            var tab = GetTabManager(window).Tabs.Single();

            await CloseTabAsync(window, tab);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(GetTabManager(window).Tabs);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ClosingUntitledTabWithContent_Save_WritesFileAndClosesTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var savePath = Path.Combine(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        // MainWindow's constructor (WireAppCommands) overwrites FileDialogService with the real
        // Avalonia one, so the stub must be installed after construction, not before.
        ctx.AppCommands.FileDialogService = new StubFileDialogService(savePath);
        window.Show();
        window.ShowSaveDiscardCancelDialogAsync = (_, _) => Task.FromResult(SaveDiscardCancelChoice.Save);
        try
        {
            FileNew(window);
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();
            var tab = GetTabManager(window).Tabs.Single();

            await CloseTabAsync(window, tab);
            Dispatcher.UIThread.RunJobs();

            Assert.True(File.Exists(savePath));
            Assert.Contains("Walk", File.ReadAllText(savePath));
            Assert.Empty(GetTabManager(window).Tabs);
            Assert.False(ctx.IoManager.RecoveryFileExists());
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ClosingUntitledTabWithContent_SaveThenCancelFilePicker_LeavesTabOpen()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        ctx.AppCommands.FileDialogService = new StubFileDialogService(null);
        window.Show();
        window.ShowSaveDiscardCancelDialogAsync = (_, _) => Task.FromResult(SaveDiscardCancelChoice.Save);
        try
        {
            FileNew(window);
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();
            var tab = GetTabManager(window).Tabs.Single();

            await CloseTabAsync(window, tab);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(GetTabManager(window).Tabs);
            Assert.Null(ctx.ProjectManager.FileName);
        }
        finally { window.Close(); }
    }
}
