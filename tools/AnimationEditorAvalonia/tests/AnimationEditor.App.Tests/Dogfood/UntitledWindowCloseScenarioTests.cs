using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Closing the window asks about every Untitled <c>.achx</c> tab that has content, with the tab-close prompt, once per
/// tab and in tab-strip order, and about file-backed tabs whose last save failed. An Untitled tab never edited (or
/// edited back to empty) closes silently. A <c>.seqj</c> has no Untitled form: a new sequence file is created on disk.
/// </summary>
public class UntitledWindowCloseScenarioTests
{
    private static string Prompt(string tabName) =>
        $"save-discard-cancel: Unsaved Changes / \"{tabName}\" has unsaved changes. Save before closing?";

    /// <summary>File &gt; New with the save dialog cancelled, then a chain with the given name in the new Untitled tab.</summary>
    private static string NewUntitledTabWithAChain(AnimationEditorHarness editor, string chainName)
    {
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type(chainName);
        editor.Press(Key.Enter);
        editor.Project.AnimationChains.Single().Name.ShouldBe(chainName);
        return editor.Tabs.ActiveTab!.DisplayName;
    }

    private static async Task<string> OpenHeroAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        return hero;
    }

    /// <summary>The Save / Don't Save / Cancel prompts shown so far (File > New's own save dialog is not one).</summary>
    private static List<string> Prompts(AnimationEditorHarness editor) =>
        editor.Dialogs.Shown.Where(shown => shown.StartsWith("save-discard-cancel")).ToList();

    /// <summary>Asks the window to close, as the title bar's X does, and lets the prompts run.</summary>
    private static async Task CloseWindowAsync(AnimationEditorHarness editor, int expectedPrompts)
    {
        editor.Window.Close();
        if (expectedPrompts > 0)
            await editor.WaitUntilAsync(() => Prompts(editor).Count >= expectedPrompts, TimeSpan.FromSeconds(5));
        await editor.WaitUntilAsync(() => !editor.Window.IsVisible, TimeSpan.FromMilliseconds(500));
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WithAnUntitledTabWithContent_Asks_AndCancelKeepsTheWindowOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroAsync(editor);
        string untitled = NewUntitledTabWithAChain(editor, "Idle");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

        await CloseWindowAsync(editor, 1);

        Prompts(editor).ShouldBe(new[] { Prompt(untitled) });
        editor.Window.IsVisible.ShouldBeTrue("Cancel keeps the window open");
        editor.Tabs.Tabs.Count.ShouldBe(2);
        editor.Project.AnimationChains.Single().Name.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WithAnUntitledTabWithContent_Discard_Closes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroAsync(editor);
        string untitled = NewUntitledTabWithAChain(editor, "Idle");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

        await CloseWindowAsync(editor, 1);

        Prompts(editor).ShouldBe(new[] { Prompt(untitled) });
        editor.Window.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WithAnUntitledTabWithContent_Save_WritesWhereTheDialogSays_AndCloses()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroAsync(editor);
        NewUntitledTabWithAChain(editor, "Idle");
        string target = Path.Combine(editor.ProjectFolder, "saved.achx");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(target);

        await CloseWindowAsync(editor, 1);

        AnimationEditorHarness.ReadSaved(target).AnimationChains.Single().Name.ShouldBe("Idle");
        editor.Window.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_Save_ThenCancellingTheSaveAsDialog_KeepsEverythingOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroAsync(editor);
        NewUntitledTabWithAChain(editor, "Idle");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(null);

        await CloseWindowAsync(editor, 1);

        editor.Window.IsVisible.ShouldBeTrue("a cancelled Save As cancels the close");
        editor.Tabs.Tabs.Count.ShouldBe(2);
        editor.Services.ProjectManager.FileName.ShouldBeNullOrEmpty();
        editor.Project.AnimationChains.Single().Name.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WithAnUntouchedUntitledTab_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroAsync(editor);
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Tabs.Tabs.Count.ShouldBe(2);

        await CloseWindowAsync(editor, 0);

        Prompts(editor).ShouldBeEmpty();
        editor.Window.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WithTwoUntitledTabs_AsksForEach_InTabOrder()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroAsync(editor);
        string first = NewUntitledTabWithAChain(editor, "Idle");
        string second = NewUntitledTabWithAChain(editor, "Run");
        first.ShouldNotBe(second);
        editor.ClickTab(second);
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

        await CloseWindowAsync(editor, 2);

        Prompts(editor).ShouldBe(new[] { Prompt(first), Prompt(second) });
        editor.Window.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WithTwoUntitledTabs_CancelOnTheFirstStopsTheSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenHeroAsync(editor);
        string first = NewUntitledTabWithAChain(editor, "Idle");
        NewUntitledTabWithAChain(editor, "Run");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

        await CloseWindowAsync(editor, 1);

        Prompts(editor).ShouldBe(new[] { Prompt(first) });
        editor.Window.IsVisible.ShouldBeTrue();
        editor.Tabs.Tabs.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WithAFailedSaveTabAndAnUntitledTab_AsksForBoth_InTabOrder()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = await OpenHeroAsync(editor);
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        try
        {
            AnimationChainSave walk = editor.ChainNamed("Walk");
            editor.Expand(walk);
            editor.ClickRow(walk.Frames[0]);
            editor.TypeNumber("PropPixelX", "8");
            string untitled = NewUntitledTabWithAChain(editor, "Idle");
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

            await CloseWindowAsync(editor, 2);

            Prompts(editor).ShouldBe(new[] { Prompt("hero.achx"), Prompt(untitled) });
            editor.Window.IsVisible.ShouldBeFalse();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }
}
