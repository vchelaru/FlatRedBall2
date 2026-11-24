using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A file-backed <c>.achx</c> tab whose last save failed (read-only file, disk full) holds edits the file lacks, so
/// closing the tab, Detach to New Window and closing the window ask Save / Don't Save / Cancel, like a <c>.seqj</c>
/// tab (<see cref="SequenceParityScenarioTests"/>) and an Untitled tab do. A tab that saved fine closes silently.
/// </summary>
public class AchxFailedSaveScenarioTests
{
    private const string AutoSaveFailedPrefix = "Auto save failed — ";

    private static string Prompt(string file) =>
        $"save-discard-cancel: Unsaved Changes / \"{file}\" has unsaved changes. Save before closing?";

    /// <summary>Opens <paramref name="name"/> and makes the first frame's X edit, which fails to save when the file is read-only.</summary>
    private static async Task<string> OpenAsync(AnimationEditorHarness editor, string name)
    {
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx(name, AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        return path;
    }

    private static void EditFirstFrame(AnimationEditorHarness editor, string x)
    {
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.TypeNumber("PropPixelX", x);
    }

    private static async Task<string> OpenAndFailASave(AnimationEditorHarness editor, string name = "hero.achx")
    {
        string path = await OpenAsync(editor, name);
        UnusualProjectScenarioTests.MakeUnwritable(path);
        EditFirstFrame(editor, "8");
        return path;
    }

    /// <summary>
    /// Opens hero.achx and boss.achx (both written first: on Linux an unwritable file means an unwritable folder), fails a
    /// save on hero, and leaves boss in front. The caller makes hero writable again.
    /// </summary>
    private static async Task<(string Hero, string Boss)> OpenTwoTabsAndFailTheFirstSave(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        await editor.OpenAsync(boss);
        editor.ClickTab("hero.achx");
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        EditFirstFrame(editor, "8");
        editor.ClickTab("boss.achx");
        return (hero, boss);
    }

    private static int SavedX(string path) =>
        (int)AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().LeftCoordinate;

    [AvaloniaFact]
    public async Task FailedSave_StillShowsTheAutoSaveFailedProblemBanner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.ProblemText.ShouldNotBeNull();
            editor.ProblemText!.ShouldStartWith(AutoSaveFailedPrefix);
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Asks_AndCancelKeepsTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            editor.CloseTab("hero.achx");

            editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
            editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8, "the unsaved edit is still there");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Discard_ClosesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

            editor.CloseTab("hero.achx");

            editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
            editor.TabLabels.ShouldBeEmpty();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
        SavedX(path).ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Save_StillUnwritable_KeepsTheTabAndSaysWhy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);

            editor.CloseTab("hero.achx");

            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
            editor.ProblemText!.ShouldStartWith(AutoSaveFailedPrefix);
            editor.Dialogs.Shown.Count.ShouldBe(1);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task ClosingATabWithAFailedSave_Save_OnceWritable_SavesAndCloses()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        UnusualProjectScenarioTests.MakeWritable(path);
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);

        editor.CloseTab("hero.achx");

        editor.TabLabels.ShouldBeEmpty();
        SavedX(path).ShouldBe(8);
    }

    [AvaloniaFact]
    public async Task ClosingATabThatSavedFine_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAsync(editor, "hero.achx");
        EditFirstFrame(editor, "8");

        editor.CloseTab("hero.achx");

        editor.TabLabels.ShouldBeEmpty();
        editor.Dialogs.Shown.ShouldBeEmpty();
        SavedX(path).ShouldBe(8);
    }

    [AvaloniaFact]
    public async Task ClosingATab_AfterALaterEditSavedAgain_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        UnusualProjectScenarioTests.MakeWritable(path);
        editor.TypeNumber("PropPixelX", "16");

        editor.CloseTab("hero.achx");

        editor.TabLabels.ShouldBeEmpty();
        editor.Dialogs.Shown.ShouldBeEmpty();
        SavedX(path).ShouldBe(16);
    }

    [AvaloniaFact]
    public async Task ClosingATab_AfterReloadFromDisk_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        UnusualProjectScenarioTests.MakeWritable(path);
        editor.ClickMenu("MenuReloadFromDisk");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.CloseTab("hero.achx");

        editor.TabLabels.ShouldBeEmpty();
        editor.Dialogs.Shown.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ABackgroundTabWithAFailedSave_StillAsksWhenClosedFromTheStrip_AndSaveRetriesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, _) = await OpenTwoTabsAndFailTheFirstSave(editor);
        try
        {
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On", "the other tab saved fine");
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
            UnusualProjectScenarioTests.MakeWritable(hero);

            editor.CloseTab("hero.achx");

            editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
            editor.TabLabels.ShouldBe(new[] { "boss.achx" });
            SavedX(hero).ShouldBe(8);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaFact]
    public async Task SwitchingBackToATabWithAFailedSave_ShowsItAsFailedStill()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, _) = await OpenTwoTabsAndFailTheFirstSave(editor);
        try
        {
            editor.ClickTab("hero.achx");

            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");
            editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaFact]
    public async Task SwitchingBackToATabWithAFailedSave_WhoseFileChanged_KeepsTheEditAndTheFileUntilAReload()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, _) = await OpenTwoTabsAndFailTheFirstSave(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (32, 0, 16, 16)));

        editor.ClickTab("hero.achx");

        editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8, "the unsaved edit is kept");
        editor.ProblemText!.ShouldContain("\"hero.achx\" changed on disk");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");

        EditFirstFrame(editor, "12");
        SavedX(hero).ShouldBe(32, "the next edit must not overwrite the newer file");

        editor.ClickMenu("MenuReloadFromDisk");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        EditFirstFrame(editor, "4");
        SavedX(hero).ShouldBe(4);
    }

    [AvaloniaFact]
    public async Task CuttingFromATabWithAFailedSaveIntoAnother_SavesIt_SoClosingDoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, _) = await OpenTwoTabsAndFailTheFirstSave(editor);
        editor.ClickTab("hero.achx");
        UnusualProjectScenarioTests.MakeWritable(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.X, RawInputModifiers.Control);
        editor.ClickTab("boss.achx");
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.WaitUntil(() => editor.Project.AnimationChains.Count == 2, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        AnimationEditorHarness.ReadSaved(hero).AnimationChains.ShouldBeEmpty();

        editor.ClickTab("hero.achx");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        editor.CloseTab("hero.achx");

        editor.Dialogs.Shown.ShouldBeEmpty();
    }

    // A clean tab's cut must not overwrite an outside edit either: the tab has not read it yet.
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CuttingFromATabWhoseFileChanged_RefusesToWriteIt_AndClosingAsks(bool failedSave)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, _) = await OpenTwoTabsAndFailTheFirstSave(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        editor.ClickTab("hero.achx");
        if (!failedSave) EditFirstFrame(editor, "16");
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.X, RawInputModifiers.Control);
        editor.ClickTab("boss.achx");
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (32, 0, 16, 16)));
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.WaitUntil(() => editor.Project.AnimationChains.Count == 2, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        SavedX(hero).ShouldBe(32);
        editor.ProblemText!.ShouldContain("\"hero.achx\" changed on disk");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

        editor.CloseTab("hero.achx");

        editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
    }

    [AvaloniaFact]
    public async Task SaveAsOverAnotherTabsFile_ThatTabShowsTheSavedContent()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, "hero.achx");
        string boss = await OpenAsync(editor, "boss.achx");
        editor.ClickTab("hero.achx");
        EditFirstFrame(editor, "8");
        editor.Dialogs.AnswerNextSaveFile(boss);
        editor.ClickMenu("MenuSaveAs");
        editor.WaitUntil(() => SavedX(boss) == 8, TimeSpan.FromSeconds(5)).ShouldBeTrue();

        editor.ClickTab("boss.achx");

        editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8);
    }

    [AvaloniaFact]
    public async Task LeavingATabRightAfterAnExternalEdit_ShowsTheEditOnReturn()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string hero = await OpenAsync(editor, "hero.achx");
        await OpenAsync(editor, "boss.achx");
        editor.ClickTab("hero.achx");
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (32, 0, 16, 16)));

        editor.ClickTab("boss.achx");
        editor.ClickTab("hero.achx");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(32);
    }

    [AvaloniaFact]
    public async Task DetachToNewWindow_WithAFailedSave_Asks_AndCancelKeepsTheTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);
            MenuItem detach = editor.TabFor("hero.achx").ContextMenu!.Items.OfType<MenuItem>()
                .Single(item => (string?)item.Header == "Detach to New Window");

            detach.Command!.Execute(null);
            await editor.WaitUntilAsync(() => editor.Dialogs.Shown.Count > 0, TimeSpan.FromSeconds(5));
            editor.Layout();

            editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task DetachToNewWindow_AfterASuccessfulSave_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAsync(editor, "hero.achx");
        EditFirstFrame(editor, "8");
        MainWindow? detached = null;
        string temp = Path.Combine(Path.GetTempPath(), "AnimationEditorTests", Guid.NewGuid().ToString("N"));
        editor.Window.DetachedWindowFactory = () =>
            detached = App.CreateDetachedWindow(temp, Path.Combine(temp, "recovery.achx"));
        MenuItem detach = editor.TabFor("hero.achx").ContextMenu!.Items.OfType<MenuItem>()
            .Single(item => (string?)item.Header == "Detach to New Window");
        try
        {
            detach.Command!.Execute(null);
            await editor.WaitUntilAsync(() => detached is not null, TimeSpan.FromSeconds(10));

            editor.TabLabels.ShouldBeEmpty();
            editor.Dialogs.Shown.ShouldBeEmpty();
        }
        finally
        {
            detached?.CloseWithoutPrompt();
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowWithAFailedSave_Asks_AndCancelKeepsTheWindowOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            editor.Window.Close();
            await editor.WaitUntilAsync(() => editor.Dialogs.Shown.Count > 0, TimeSpan.FromSeconds(5));
            editor.Layout();

            editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
            editor.Window.IsVisible.ShouldBeTrue("Cancel keeps the window open");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowWithAFailedSave_Discard_ClosesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

            editor.Window.Close();
            await editor.WaitUntilAsync(() => !editor.Window.IsVisible, TimeSpan.FromSeconds(5));

            editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
            editor.Window.IsVisible.ShouldBeFalse();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowWithAFailedSave_Save_StillUnwritable_KeepsTheWindowOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);

            editor.Window.Close();
            await editor.WaitUntilAsync(() => editor.Dialogs.Shown.Count > 0, TimeSpan.FromSeconds(5));
            editor.Layout();

            editor.Window.IsVisible.ShouldBeTrue("a failed retry keeps everything open");
            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowWithAFailedSave_Save_OnceWritable_SavesAndCloses()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAndFailASave(editor);
        UnusualProjectScenarioTests.MakeWritable(path);
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);

        editor.Window.Close();
        await editor.WaitUntilAsync(() => !editor.Window.IsVisible, TimeSpan.FromSeconds(5));

        editor.Window.IsVisible.ShouldBeFalse();
        SavedX(path).ShouldBe(8);
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowAfterEverythingSaved_DoesNotAsk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, "hero.achx");
        EditFirstFrame(editor, "8");

        editor.Window.Close();
        editor.Layout();

        editor.Window.IsVisible.ShouldBeFalse();
        editor.Dialogs.Shown.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ClosingTheWindowWithSeveralFailedTabs_AsksOncePerTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        (string hero, string boss) = await OpenTwoTabsAndFailTheFirstSave(editor);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            EditFirstFrame(editor, "8");
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Discard);

            editor.Window.Close();
            await editor.WaitUntilAsync(() => !editor.Window.IsVisible, TimeSpan.FromSeconds(5));

            editor.Dialogs.Shown.OrderBy(shown => shown).ShouldBe(new[] { Prompt("boss.achx"), Prompt("hero.achx") });
            editor.Window.IsVisible.ShouldBeFalse();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
    }

    [AvaloniaFact]
    public async Task ASaveThatIsPausedBecauseTheFileChangedOnDisk_AlsoAsksOnClose()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await OpenAsync(editor, "hero.achx");
        File.WriteAllText(path, "<AnimationChainArrayS");
        editor.WaitUntil(() => editor.ProblemText?.Contains("Reload skipped") == true, TimeSpan.FromSeconds(5))
            .ShouldBeTrue("the editor reports the file it could not reload");
        EditFirstFrame(editor, "8");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed", "saving is paused until the file reloads");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

        editor.CloseTab("hero.achx");

        editor.Dialogs.Shown.ShouldBe(new[] { Prompt("hero.achx") });
        editor.TabLabels.ShouldBe(new[] { "hero.achx" });
    }
}
