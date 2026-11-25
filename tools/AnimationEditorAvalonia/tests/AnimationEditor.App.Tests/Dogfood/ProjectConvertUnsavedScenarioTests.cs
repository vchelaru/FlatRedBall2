using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Views.Controls;
using AnimationEditor.Views.Dialogs;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The Animations tab's Convert to .achj on a file whose tab holds edits its last save could not write: they are saved
/// first, and a file whose save is refused again is left alone and named in the problem banner.
/// </summary>
public class ProjectConvertUnsavedScenarioTests
{
    /// <summary>Opens boss.achx and hero.achx from a project folder's Sprites folder, and fails the save of an X edit on hero, which stays in front.</summary>
    private static async Task<(string Hero, string Boss, List<string> Recycled)> OpenAndFailASaveAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "Sprites"));
        string hero = editor.WriteAchx("Sprites/hero.achx", AnimationEditorHarness.Chain("Walk", "../sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteAchx("Sprites/boss.achx", AnimationEditorHarness.Chain("Walk", "../sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        List<string> recycled = new List<string>();
        editor.Window.DeleteToRecycleBin = path =>
        {
            recycled.Add(Path.GetFileName(path));
            File.Delete(path);
            return null;
        };
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        editor.Services.AppCommands.SetFramePixelRegion(
            editor.ChainNamed("Walk").Frames, pixelX: 8, pixelY: null, pixelW: null, pixelH: null);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.ProblemText!.ShouldStartWith("Auto save failed");
        return (hero, boss, recycled);
    }

    private static void PickFromRow(AnimationEditorHarness editor, string row, string item, bool confirm = true)
    {
        ProjectPanelControl panel = editor.Control<ProjectPanelControl>("ProjectPanel");
        TreeViewItem target = PanelsScenarioTests.RowFor(editor, panel, row);
        editor.RightClickAt(target.TranslatePoint(new Point(target.Bounds.Width / 2, 8), editor.Window)!.Value);
        editor.Dialogs.Shown.Clear();
        editor.Dialogs.AnswerNextConfirm(confirm);
        editor.PickMenuItem(panel.ProjectTree.ContextMenu!, item);
        editor.Wait(TimeSpan.FromMilliseconds(100));
    }

    private static int SavedX(string path) =>
        (int)AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().LeftCoordinate;

    // Right-clicking a file row brings its tab to the front, so only a folder convert finds the tab behind.
    [AvaloniaFact]
    public async Task ConvertFolder_TabBehindWithAFailedSave_SavesItFirst()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        editor.ClickTab("boss.achx");

        PickFromRow(editor, "Sprites", "Convert Animations to .achj");

        SavedX(Path.ChangeExtension(hero, ".achj")).ShouldBe(8);
        recycled.ShouldBe(new[] { "boss.achx", "hero.achx" });
        editor.Tabs.Tabs.ShouldBeEmpty();
        editor.ProblemText.ShouldBeNull("the failed save it reported is done");
    }

    [AvaloniaFact]
    public async Task ConvertFile_TabWithAFailedSave_SavesItFirst_AndAsksNothingMore()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);

        PickFromRow(editor, "hero.achx", "Convert to .achj");

        SavedX(Path.ChangeExtension(hero, ".achj")).ShouldBe(8);
        recycled.ShouldBe(new[] { "hero.achx" });
        editor.TabLabels.ShouldBe(new[] { "boss.achx" });
        editor.Dialogs.Shown.Count.ShouldBe(1, "only the confirm: " + string.Join("; ", editor.Dialogs.Shown));
        editor.ProblemText.ShouldBeNull("the failed save it reported is done");
    }

    // Only a banner about a failed save that the convert made good is cleared.
    [AvaloniaTheory]
    [InlineData("boss.achx", "Auto save failed — Access to the path is denied.")]
    [InlineData("hero.achx", "Something else is wrong.")]
    public async Task ConvertFile_KeepsABannerItDidNotSettle(string convert, string banner)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        editor.Window.ShowProblem(banner);

        PickFromRow(editor, convert, "Convert to .achj");

        recycled.ShouldBe(new[] { convert });
        editor.ProblemText.ShouldBe(banner);
    }

    [AvaloniaFact]
    public async Task ConvertFile_TabWhoseFileChangedOnDisk_IsNotConverted()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        File.AppendAllText(hero, "<!-- edited elsewhere -->");
        string heroText = File.ReadAllText(hero);

        PickFromRow(editor, "hero.achx", "Convert to .achj");

        // Long enough for the live watcher to see the change, which the convert's banner already reports.
        editor.Wait(TimeSpan.FromSeconds(2));
        editor.ProblemText!.ShouldStartWith("Converted 0, 1 problem: hero.achx: not converted, its unsaved edits could not be saved (");
        File.ReadAllText(hero).ShouldBe(heroText);
        File.Exists(Path.ChangeExtension(hero, ".achj")).ShouldBeFalse();
        recycled.ShouldBeEmpty();

        File.AppendAllText(hero, "<!-- edited again -->");
        editor.WaitUntil(() => editor.ProblemText?.StartsWith("Auto save failed") == true, TimeSpan.FromSeconds(5))
            .ShouldBeTrue("a change nothing reported yet is still reported");
    }

    // Skipped, so its tab stays open, and must know the edits are saved now.
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Convert_TabWithAFailedSave_WhoseAchjExists_IsSaved_AndItsTabKnows(bool folderWithTheTabBehind)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        File.WriteAllText(Path.ChangeExtension(hero, ".achj"), "another file");

        if (folderWithTheTabBehind)
        {
            editor.ClickTab("boss.achx");
            PickFromRow(editor, "Sprites", "Convert Animations to .achj");
        }
        else
        {
            PickFromRow(editor, "hero.achx", "Convert to .achj");
        }

        File.ReadAllText(Path.ChangeExtension(hero, ".achj")).ShouldBe("another file");
        recycled.ShouldNotContain("hero.achx");
        SavedX(hero).ShouldBe(8);
        editor.ClickTab("hero.achx");
        editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8);
        editor.UndoManager.SaveState.ShouldBe(SaveState.AutoSaveOn);
        editor.Dialogs.Shown.Clear();
        editor.CloseTab("hero.achx");
        editor.Dialogs.Shown.ShouldBeEmpty();
    }

    // Closing a tab behind the front one leaves the front document, its failed save and its undo alone.
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ClosingATabBehind_KeepsTheFrontTabsUnsavedEdits(bool byFolderConvert, bool changedOnDisk)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, _) = await OpenAndFailASaveAsync(editor);
        try
        {
            if (changedOnDisk)
            {
                UnusualProjectScenarioTests.MakeWritable(hero);
                File.AppendAllText(hero, "<!-- edited elsewhere -->");
            }
            // On Linux a file is made unwritable through its folder, which would stop boss.achj too.
            else if (byFolderConvert && !OperatingSystem.IsWindows())
            {
                return;
            }

            if (byFolderConvert)
                PickFromRow(editor, "Sprites", "Convert Animations to .achj");
            else
                editor.CloseTab("boss.achx");
            editor.Wait(TimeSpan.FromMilliseconds(100));

            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
            editor.UndoManager.SaveState.ShouldBe(SaveState.Failed);
            editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8, "the unsaved edit is still there");
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);
            editor.CloseTab("hero.achx");
            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
            editor.Press(Key.Z, RawInputModifiers.Control);
            editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(0, "its undo history is still there");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaFact]
    public async Task ConvertFile_TabWhoseSaveFailsAgain_IsNotConverted_AndIsNamed()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        try
        {
            PickFromRow(editor, "hero.achx", "Convert to .achj");

            editor.ProblemText!.ShouldStartWith("Converted 0, 1 problem: hero.achx: not converted, its unsaved edits could not be saved (");
            File.Exists(Path.ChangeExtension(hero, ".achj")).ShouldBeFalse();
            recycled.ShouldBeEmpty();
            editor.TabLabels.ShouldContain("hero.achx");
            editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8, "the unsaved edit is still there");
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaFact]
    public async Task ConvertFolder_TabWhoseFileChangedOnDisk_IsNotConverted_AndIsNamed_TheRestConvert()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, boss, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        editor.ClickTab("boss.achx");
        File.AppendAllText(hero, "<!-- edited elsewhere -->");
        string heroText = File.ReadAllText(hero);

        PickFromRow(editor, "Sprites", "Convert Animations to .achj");

        editor.ProblemText!.ShouldStartWith("Converted 1, 1 problem: hero.achx: not converted, its unsaved edits could not be saved (");
        File.ReadAllText(hero).ShouldBe(heroText);
        File.Exists(Path.ChangeExtension(hero, ".achj")).ShouldBeFalse();
        File.Exists(Path.ChangeExtension(boss, ".achj")).ShouldBeTrue();
        recycled.ShouldBe(new[] { "boss.achx" });
        editor.WaitUntil(() => editor.TabLabels.SequenceEqual(new[] { "hero.achx" }), TimeSpan.FromSeconds(5)).ShouldBeTrue();
        editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(8, "the unsaved edit is still there");
    }

    [AvaloniaFact]
    public async Task ConvertFile_ConfirmDeclined_SavesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);

        PickFromRow(editor, "hero.achx", "Convert to .achj", confirm: false);

        SavedX(hero).ShouldBe(0);
        recycled.ShouldBeEmpty();
        editor.TabLabels.ShouldContain("hero.achx");
    }
}
