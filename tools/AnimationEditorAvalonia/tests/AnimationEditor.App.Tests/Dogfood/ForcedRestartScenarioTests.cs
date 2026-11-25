using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Shouldly;
using System.Reflection;
using System.Text.Json;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The process ending without the user closing the window: a Windows shutdown or the restart that installs an update.</summary>
public class ForcedRestartScenarioTests
{
    [AvaloniaFact]
    public void OsShutdown_WithAnUntitledDocument_LeavesItRecoverable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.ClickMenu("MenuNew");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Services.AppCommands.AddAnimationChainWithName("Unsaved");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        string recovery = editor.Services.IoManager.RecoveryFilePath;
        File.Exists(recovery).ShouldBeTrue();

        // What the platform calls on WM_ENDSESSION; no prompt is possible then.
        MethodInfo close = typeof(Window).GetMethod("CloseCore", BindingFlags.Instance | BindingFlags.NonPublic)!;
        close.Invoke(editor.Window, [WindowCloseReason.OSShutdown, false, false]);
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.Dialogs.Shown.ShouldNotContain(shown => shown.StartsWith("save-discard-cancel"));
        File.Exists(recovery).ShouldBeTrue("nobody discarded the document");
    }

    [AvaloniaFact]
    public async Task RestartToUpdate_WithAFailedSave_AsksFirst()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        try
        {
            editor.Services.AppCommands.AddAnimationChainWithName("Unsaved");
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            editor.Window.FindControl<Button>("RestartForUpdateBtn")!.RaiseEvent(
                new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            editor.Wait(TimeSpan.FromMilliseconds(100));

            ((FakeApplicationUpdater)editor.Services.ApplicationUpdater).RestartCount.ShouldBe(0, "the restart drops the edits");
            editor.Dialogs.Shown.ShouldContain(shown => shown.StartsWith("save-discard-cancel"));
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
            editor.Window.CloseWithoutPrompt();
        }
    }

    private static void NewUntitledWith(AnimationEditorHarness editor, string chain)
    {
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.ClickMenu("MenuNew");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Services.AppCommands.AddAnimationChainWithName(chain);
        editor.Wait(TimeSpan.FromMilliseconds(50));
    }

    private static FakeApplicationUpdater ClickRestart(AnimationEditorHarness editor, Action? restarting = null)
    {
        FakeApplicationUpdater updater = (FakeApplicationUpdater)editor.Services.ApplicationUpdater;
        updater.Restarting = restarting;
        editor.Window.FindControl<Button>("RestartForUpdateBtn")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        editor.Wait(TimeSpan.FromMilliseconds(100));
        return updater;
    }

    [AvaloniaFact]
    public void RestartToUpdate_AsksAboutAnotherWindowsDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        using AnimationEditorHarness other = new AnimationEditorHarness();
        editor.Window.OpenEditorWindows = () => [editor.Window, other.Window];
        NewUntitledWith(other, "Unsaved");
        other.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

        int restarts = ClickRestart(editor).RestartCount;

        other.Window.CloseWithoutPrompt();
        restarts.ShouldBe(0, "the restart ends the other window too");
    }

    [AvaloniaFact]
    public void RestartToUpdate_AfterDontSave_DoesNotBringTheDocumentBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        NewUntitledWith(editor, "Unsaved");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
        bool? recoverable = null;

        ClickRestart(editor, () => recoverable = editor.Services.IoManager.RecoveryFileExists());

        editor.Window.CloseWithoutPrompt();
        recoverable.ShouldBe(false);
    }

    [AvaloniaFact]
    public void RestartToUpdate_ThatFailsAfterDontSave_KeepsTheOpenDocumentRecoverable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        NewUntitledWith(editor, "Unsaved");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

        ClickRestart(editor, () => throw new IOException("the update is in use"));

        bool recoverable = editor.Services.IoManager.RecoveryFileExists();
        List<string> tabs = editor.TabLabels.ToList();
        editor.Window.CloseWithoutPrompt();
        tabs.ShouldBe(new[] { "Untitled" });
        recoverable.ShouldBeTrue("the document is still open");
    }

    [AvaloniaFact]
    public void RestartToUpdate_AfterSavingTheUntitledDocument_RestoresItsTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        NewUntitledWith(editor, "Walk");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(Path.Combine(editor.ProjectFolder, "hero.achx"));
        string? settings = null;

        ClickRestart(editor, () => settings = ReadSettings(editor));

        editor.Window.CloseWithoutPrompt();
        SavedTabs(settings).ShouldBe(new[] { "hero.achx" });
    }

    [AvaloniaFact]
    public void ClosingTheWindow_AfterSavingTheUntitledDocument_RestoresItsTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        NewUntitledWith(editor, "Walk");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(Path.Combine(editor.ProjectFolder, "hero.achx"));
        string? settings = null;
        editor.Window.Closed += (_, _) => settings = ReadSettings(editor);

        editor.Window.Close();
        editor.Wait(TimeSpan.FromMilliseconds(100));

        SavedTabs(settings).ShouldBe(new[] { "hero.achx" });
    }

    private static string ReadSettings(AnimationEditorHarness editor) =>
        File.ReadAllText(AppSettingsLocation.ForApplicationDataRoot(editor.Services.SettingsRoot).FullPath);

    private static IEnumerable<string?> SavedTabs(string? settings) =>
        JsonSerializer.Deserialize<AppSettingsModel>(settings.ShouldNotBeNull())!.OpenTabPaths.Select(path => Path.GetFileName(path));
}
