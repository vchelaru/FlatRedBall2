using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A fresh editor with nothing open: the untitled document takes edits, Ctrl+S asks where to
/// save, and a restart with the same settings reopens what was open.
/// </summary>
public class UntitledScenarioTests
{
    [AvaloniaFact]
    public void FreshEditor_AddChainThenCtrlS_AsksForAPath_AndWritesTheFile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string target = Path.Combine(editor.ProjectFolder, "new.achx");
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Idle");
        editor.Press(Key.Enter);
        editor.Project.AnimationChains.Single().Name.ShouldBe("Idle");

        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.S, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        File.Exists(target).ShouldBeTrue();
        AnimationEditorHarness.ReadSaved(target).AnimationChains.Single().Name.ShouldBe("Idle");
        editor.Services.ProjectManager.FileName.ShouldBe(target);
        editor.TitleFileName.ShouldContain("new");
        editor.TabLabels.ShouldContain(label => label.Contains("new"));
    }

    [AvaloniaFact]
    public void FreshEditor_CtrlS_ThenCancel_StaysUntitledWithoutAnError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Press(Key.Enter);

        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.S, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Services.ProjectManager.FileName.ShouldBeNullOrEmpty();
        editor.Project.AnimationChains.Count.ShouldBe(1);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task Restart_WithTheSameSettings_ReopensTheLastFile()
    {
        string settingsRoot = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", "settings-" + Guid.NewGuid().ToString("N"));
        string kept = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", "keep-" + Guid.NewGuid().ToString("N") + ".achx");
        string path;
        using (AnimationEditorHarness first = new AnimationEditorHarness(settingsRoot))
        {
            first.WritePng("sheet.png", 64, 64);
            path = first.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
            await first.OpenAsync(path);
            first.Tabs.Tabs.Count.ShouldBe(1);
            // The first harness deletes its project folder on dispose; keep a copy to put back.
            File.Copy(path, kept);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(kept, path, overwrite: true);
        File.Delete(kept);

        using AnimationEditorHarness second = new AnimationEditorHarness(settingsRoot);
        second.WaitUntil(() => second.Tabs.Tabs.Count == 1, TimeSpan.FromSeconds(3))
            .ShouldBeTrue("the restarted editor reopens the tab that was open");

        second.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero.achx");
        second.Nodes.Select(node => node.Header).ShouldContain("Walk");
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
