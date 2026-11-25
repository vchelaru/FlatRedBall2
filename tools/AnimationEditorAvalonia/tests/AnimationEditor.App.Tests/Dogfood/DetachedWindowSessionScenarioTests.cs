using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using System.Text.Json;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Detach to New Window moves one tab out. The new window shares the per-user settings and crash-recovery file with the
/// first one, as in the installed editor, and must not take the first window's session or its unsaved document.
/// </summary>
public class DetachedWindowSessionScenarioTests
{
    private static List<string> TabNames(MainWindow window) =>
        ((TabManager)typeof(MainWindow).GetField("_tabManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(window)!).Tabs.Select(tab => tab.DisplayName).ToList();

    private static List<string?> SavedTabs(AnimationEditorHarness editor) =>
        JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(
            AppSettingsLocation.ForApplicationDataRoot(editor.Services.SettingsRoot).FullPath))!
            .OpenTabPaths.Select(path => Path.GetFileName(path)).ToList()!;

    /// <summary>Opens boss and hero (after an Untitled document with content, when asked) and detaches hero.</summary>
    private static async Task<MainWindow> DetachHeroAsync(AnimationEditorHarness editor, bool untitled)
    {
        editor.WritePng("sheet.png", 64, 64);
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        if (untitled)
        {
            editor.Dialogs.AnswerNextSaveFile(null);
            editor.ClickMenu("MenuNew");
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.Services.AppCommands.AddAnimationChainWithName("Unsaved");
            editor.Wait(TimeSpan.FromMilliseconds(50));
            File.Exists(editor.Services.IoManager.RecoveryFilePath).ShouldBeTrue();
        }
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        MainWindow? detached = null;
        editor.Window.DetachedWindowFactory = () =>
            detached = App.CreateDetachedWindow(editor.Services.SettingsRoot, editor.Services.IoManager.RecoveryFilePath);
        MenuItem detach = editor.TabFor("hero.achx").ContextMenu!.Items.OfType<MenuItem>()
            .Single(item => (string?)item.Header == "Detach to New Window");
        detach.Command!.Execute(null);
        await editor.WaitUntilAsync(() => detached is not null, TimeSpan.FromSeconds(10));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(500));
        return detached!;
    }

    private static void Close(params MainWindow[] windows)
    {
        foreach (MainWindow window in windows)
        {
            window.CloseWithoutPrompt();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public async Task Detaching_OpensOnlyThatFileInTheNewWindow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        MainWindow detached = await DetachHeroAsync(editor, untitled: false);

        List<string> detachedTabs = TabNames(detached);
        List<string> firstTabs = editor.TabLabels.ToList();
        Close(detached, editor.Window);

        detachedTabs.ShouldBe(new[] { "hero.achx" });
        firstTabs.ShouldBe(new[] { "boss.achx" });
    }

    [AvaloniaFact]
    public async Task Detaching_LeavesTheUntitledDocumentsRecoveryFileAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        MainWindow detached = await DetachHeroAsync(editor, untitled: true);

        List<string> detachedTabs = TabNames(detached);
        bool recoverable = File.Exists(editor.Services.IoManager.RecoveryFilePath);
        Close(detached, editor.Window);

        detachedTabs.ShouldNotContain("Untitled");
        recoverable.ShouldBeTrue("the first window's Untitled document is still unsaved");
    }

    [AvaloniaFact]
    public async Task ClosingTheDetachedWindow_KeepsTheFirstWindowsTabsForTheNextLaunch()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        MainWindow detached = await DetachHeroAsync(editor, untitled: false);

        Close(detached);

        List<string?> saved = SavedTabs(editor);
        Close(editor.Window);
        saved.ShouldContain("boss.achx");
    }

    [AvaloniaFact]
    public async Task ClosingTheDetachedWindow_KeepsATabTheFirstWindowOpenedSince()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        MainWindow detached = await DetachHeroAsync(editor, untitled: false);
        await editor.OpenAsync(editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Run", "sheet.png", (0, 0, 16, 16))));

        Close(detached);

        List<string?> saved = SavedTabs(editor);
        Close(editor.Window);
        saved.ShouldBe(new[] { "boss.achx", "enemy.achx" });
    }
}
