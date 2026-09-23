using AnimationEditor.Core.Models;
using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The recovered-document banner describes the recovered tab, so it follows that tab: shown
/// while it is active, gone once it is closed or saved. There is no separate dismiss button.
/// </summary>
public class RecoveryBannerScenarioTests
{
    [AvaloniaFact]
    public void Banner_RecoveredTabClosed_Hides()
    {
        using AnimationEditorHarness editor = StartWithRecoveredDocument();
        editor.Control<Control>("RecoveredDocumentBanner").IsVisible.ShouldBeTrue();

        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
        editor.CloseTab(RecoveredTabName(editor));
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Control<Control>("RecoveredDocumentBanner").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Banner_RecoveredTabSaved_Hides()
    {
        using AnimationEditorHarness editor = StartWithRecoveredDocument();
        string target = Path.Combine(editor.ProjectFolder, "saved.achx");

        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.S, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        File.Exists(target).ShouldBeTrue();
        editor.Control<Control>("RecoveredDocumentBanner").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Banner_OtherTabActive_Hides_AndReturningShowsItAgain()
    {
        using AnimationEditorHarness editor = StartWithRecoveredDocument();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string recoveredTab = RecoveredTabName(editor);

        await editor.OpenAsync(hero);
        editor.Control<Control>("RecoveredDocumentBanner").IsVisible.ShouldBeFalse("the banner describes the recovered tab, not hero.achx");

        editor.ClickTab(recoveredTab);
        editor.Control<Control>("RecoveredDocumentBanner").IsVisible.ShouldBeTrue();
    }

    private static AnimationEditorHarness StartWithRecoveredDocument()
    {
        string recoveryFile = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N") + ".achx");
        Directory.CreateDirectory(Path.GetDirectoryName(recoveryFile)!);
        AnimationChainListSave recovered = new AnimationChainListSave();
        recovered.AnimationChains.Add(new AnimationChainSave { Name = "Recovered" });
        recovered.Save(recoveryFile);
        return new AnimationEditorHarness(recoveryFilePath: recoveryFile);
    }

    private static string RecoveredTabName(AnimationEditorHarness editor) =>
        editor.Tabs.Tabs.Single(tab => TabManager.IsUntitledSentinel(tab.Path.Original)).DisplayName;
}
