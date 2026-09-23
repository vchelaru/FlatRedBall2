using AnimationEditor.Views.Dialogs;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Documents and tabs: opening two files, switching by clicking the strip, closing a dirty tab
/// through the Save / Don't Save / Cancel prompt, File &gt; New, and Save As.
/// </summary>
public class TabScenarioTests
{
    [AvaloniaFact]
    public async Task ClickingTheOtherTab_SwitchesTheTreeAndTheTitle()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        await editor.OpenAsync(enemy);
        editor.Nodes.Select(node => node.Header).ShouldContain("Bite");
        string heroTab = editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith("hero.achx")).DisplayName;

        editor.ClickTab(heroTab);

        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero.achx");
        editor.Nodes.Select(node => node.Header).ShouldContain("Walk");
        editor.Nodes.Select(node => node.Header).ShouldNotContain("Bite");
        editor.TitleFileName.ShouldContain("hero");
    }

    [AvaloniaFact]
    public async Task ClosingAFileBackedTab_AfterAnEdit_NeverPrompts_BecauseEditsAutoSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(enemy);
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.Delete);
        editor.Control<Avalonia.Controls.TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        string heroTab = editor.Tabs.ActiveTab!.DisplayName;

        editor.CloseTab(heroTab);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Dialogs.Shown.ShouldBeEmpty();
        editor.Tabs.Tabs.Select(tab => tab.Path.FullPath).ShouldAllBe(path => path.EndsWith("enemy.achx"));
        AnimationEditorHarness.ReadSaved(hero).AnimationChains.ShouldBeEmpty("the delete was auto-saved before the tab closed");
        editor.Nodes.Select(node => node.Header).ShouldContain("Bite");
    }

    [AvaloniaFact]
    public async Task ClosingAnUntitledTabWithContent_AndChoosingDontSave_DropsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string untitledTab = await OpenUntitledTabWithAChainAsync(editor);

        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
        editor.CloseTab(untitledTab);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Dialogs.Shown.ShouldContain(shown => shown.StartsWith("save-discard-cancel"));
        editor.Tabs.Tabs.Count.ShouldBe(1);
        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero.achx");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task ClosingAnUntitledTabWithContent_AndChoosingSave_WritesWhereTheDialogSays()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string untitledTab = await OpenUntitledTabWithAChainAsync(editor);
        string target = Path.Combine(editor.ProjectFolder, "saved.achx");

        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(target);
        editor.CloseTab(untitledTab);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        AnimationEditorHarness.ReadSaved(target).AnimationChains.Single().Name.ShouldBe("Idle");
        editor.Tabs.Tabs.Count.ShouldBe(1);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ClosingAnUntitledTabWithContent_AndCancelling_KeepsItOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string untitledTab = await OpenUntitledTabWithAChainAsync(editor);

        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);
        editor.CloseTab(untitledTab);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Tabs.Tabs.Count.ShouldBe(2);
        editor.Project.AnimationChains.Single().Name.ShouldBe("Idle");
    }

    /// <summary>Opens hero.achx, then File &gt; New with the save dialog cancelled, and adds a chain named Idle to the untitled tab.</summary>
    private static async Task<string> OpenUntitledTabWithAChainAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Click(editor.Control<Avalonia.Controls.Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Idle");
        editor.Press(Key.Enter);
        editor.Project.AnimationChains.Single().Name.ShouldBe("Idle");
        return editor.Tabs.ActiveTab!.DisplayName;
    }

    [AvaloniaFact]
    public async Task CtrlN_OpensAnUntitledTab_ThatSavesWhereTheDialogSays()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        string target = Path.Combine(editor.ProjectFolder, "fresh.achx");

        // File > New asks where to save the new document straight away.
        editor.Dialogs.AnswerNextSaveFile(target);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        File.Exists(target).ShouldBeTrue();
        editor.Services.ProjectManager.FileName.ShouldBe(target);
        editor.Tabs.Tabs.Count.ShouldBe(2);
        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("fresh.achx");
        editor.Project.AnimationChains.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlN_ThenCancellingTheSaveDialog_LeavesAnUntitledTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);

        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Tabs.Tabs.Count.ShouldBe(2);
        editor.Services.ProjectManager.FileName.ShouldBeNullOrEmpty();
        editor.Click(editor.Control<Avalonia.Controls.Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Project.AnimationChains.Count.ShouldBe(1, "the untitled document is editable");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task SaveAs_WritesACopy_AndTheTabFollowsTheNewPath()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        string copy = Path.Combine(editor.ProjectFolder, "hero-copy.achx");

        editor.Dialogs.AnswerNextSaveFile(copy);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        File.Exists(copy).ShouldBeTrue();
        editor.Services.ProjectManager.FileName.ShouldBe(copy);
        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero-copy.achx");
        editor.TabLabels.ShouldContain(label => label.Contains("hero-copy"));
        AnimationEditorHarness.ReadSaved(copy).AnimationChains.Single().Name.ShouldBe("Walk");
    }
}
