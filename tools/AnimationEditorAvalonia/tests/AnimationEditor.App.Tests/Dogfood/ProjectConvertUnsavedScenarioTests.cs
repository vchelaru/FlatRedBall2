using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

    [AvaloniaTheory]
    [InlineData("hero.achx")]
    [InlineData("boss.achx")]
    public async Task ConvertFile_TabWithAFailedSave_SavesItFirst_AndAsksNothingMore(string inFront)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (hero, _, recycled) = await OpenAndFailASaveAsync(editor);
        UnusualProjectScenarioTests.MakeWritable(hero);
        editor.ClickTab(inFront);

        PickFromRow(editor, "hero.achx", "Convert to .achj");

        SavedX(Path.ChangeExtension(hero, ".achj")).ShouldBe(8);
        recycled.ShouldBe(new[] { "hero.achx" });
        editor.TabLabels.ShouldBe(new[] { "boss.achx" });
        editor.Dialogs.Shown.Count.ShouldBe(1, "only the confirm: " + string.Join("; ", editor.Dialogs.Shown));
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
