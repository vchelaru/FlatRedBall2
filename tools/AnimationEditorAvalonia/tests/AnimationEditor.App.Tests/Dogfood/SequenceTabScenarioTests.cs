using System.Text.Json;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab: it opens in its own pane with the flat list of sequences, edits through the shared
/// undo history and auto-save, and keeps the hidden <c>.achx</c> document out of reach.
/// </summary>
public class SequenceTabScenarioTests
{
    private static bool SequencePaneShown(AnimationEditorHarness editor) =>
        editor.Control<Grid>("SequenceEditorPane").IsVisible
        && editor.Control<Control>("SequenceListPane").IsVisible
        && !editor.Control<Grid>("AchxEditorPane").IsVisible
        && !editor.Control<Control>("AnimationsBlock").IsVisible;

    private static List<string> SavedSequenceNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    [AvaloniaFact]
    public async Task OpeningASeqj_ShowsItsSequences_InTheSequencePane()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle", "Attack");

        await editor.OpenAsync(boss);

        SequencePaneShown(editor).ShouldBeTrue();
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack" });
        editor.TabLabels.ShouldBe(new[] { "boss.seqj" });
        editor.TitleFileName.ShouldBe("boss.seqj");
        editor.Control<TextBlock>("StatusCounts").Text.ShouldBe("2 sequences");
    }

    [AvaloniaFact]
    public async Task ReadOnlySeqj_TakesTheEdit_ShowsAutoSaveFailed_AndSavesOnceWritable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            editor.Click(editor.Control<Button>("AddSequenceButton"));

            editor.SequenceNames.ShouldBe(new[] { "Sequence", "Sequence 2" });
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");
            SavedSequenceNames(boss).ShouldBe(new[] { "Sequence" });
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }

        editor.Click(editor.Control<Button>("AddSequenceButton"));

        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On", "the next edit saves again once the file is writable");
        SavedSequenceNames(boss).ShouldBe(new[] { "Sequence", "Sequence 2", "Sequence 3" });
    }

    [AvaloniaFact]
    public async Task AddSequence_SavesTheFile_AndCtrlZTakesItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);

        editor.Click(editor.Control<Button>("AddSequenceButton"));

        editor.SequenceNames.ShouldBe(new[] { "Sequence", "Sequence 2" });
        editor.Control<ListBox>("SequenceList").SelectedIndex.ShouldBe(1);
        SavedSequenceNames(boss).ShouldBe(new[] { "Sequence", "Sequence 2" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence 2'" });

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.SequenceNames.ShouldBe(new[] { "Sequence" });
        SavedSequenceNames(boss).ShouldBe(new[] { "Sequence" });
    }

    [AvaloniaFact]
    public async Task RenameAndDelete_FromTheListMenu_AreUndoable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle", "Attack");
        await editor.OpenAsync(boss);
        ListBox list = editor.Control<ListBox>("SequenceList");
        ContextMenu menu = list.ContextMenu!;
        editor.RightClick(list.ContainerFromIndex(1)!);

        editor.Dialogs.AnswerNextPrompt("Charge");
        editor.PickMenuItem(menu, "Rename…");
        editor.Layout();
        editor.RightClick(list.ContainerFromIndex(1)!);
        editor.PickMenuItem(menu, "Delete");
        editor.Layout();

        editor.SequenceNames.ShouldBe(new[] { "Idle" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Attack' to 'Charge'", "Delete Sequence 'Charge'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Charge" });
    }

    [AvaloniaFact]
    public async Task SwitchingTabsAndBack_KeepsEachTabsOwnHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);
        List<string> heroHistory = editor.UndoLabels;
        await editor.OpenAsync(boss);
        editor.Click(editor.Control<Button>("AddSequenceButton"));

        editor.ClickTab("hero.achx");
        editor.UndoLabels.ShouldBe(heroHistory);
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
        editor.ClickTab("boss.seqj");

        SequencePaneShown(editor).ShouldBeTrue();
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence 2'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Sequence" });
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task OnASequenceTab_AchxOnlyMenuItemsAndHotkeysAreOff_UndoRedoSaveStayOn()
    {
        string[] achxItems =
        {
            "MenuExportPixiJs", "MenuExportGodot", "MenuExportGif", "MenuExportWebp",
            // Copy, Cut, Paste and Duplicate are the pane's, not the scope's: a sequence tab takes them once its list or timeline was touched.
            "MenuResizeTexture", "MenuAssociateTiledTileset",
            "MenuWireframeZoomIn", "MenuWireframeZoomOut", "MenuPreviewZoomIn", "MenuPreviewZoomOut",
        };
        string[] documentItems = { "MenuSave", "MenuSaveAs", "MenuUndo", "MenuRedo", "MenuShowHistory", "MenuReloadFromDisk" };
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Run"));
        await editor.OpenAsync(boss);
        // One step to undo and one to redo, so Undo and Redo are both live.
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        editor.Press(Key.Z, RawInputModifiers.Control);

        achxItems.Where(name => editor.Control<MenuItem>(name).IsEnabled).ShouldBeEmpty();
        documentItems.Where(name => !editor.Control<MenuItem>(name).IsEnabled).ShouldBeEmpty();
        editor.ClickMenu("MenuRedo");
        editor.SequenceNames.ShouldBe(new[] { "Sequence", "Sequence 2", "Sequence 3" });

        // Delete acts on the sequence tab's own selection, never on the achx document hidden behind it.
        editor.Press(Key.Delete);
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
    }

    [AvaloniaFact]
    public async Task NewSequenceFile_WritesAFileWithOneSequence_AndOpensIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");

        editor.Dialogs.AnswerNextSaveFile(path);
        editor.ClickMenu("MenuNewSequence");
        editor.WaitUntil(() => editor.TabLabels.Contains("boss.seqj"), TimeSpan.FromSeconds(5));

        SequencePaneShown(editor).ShouldBeTrue();
        editor.SequenceNames.ShouldBe(new[] { "Sequence" });
        SavedSequenceNames(path).ShouldBe(new[] { "Sequence" });
    }

    [AvaloniaFact]
    public async Task ProjectPanel_ListsSeqjFiles_AndDoubleClickOpensOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        editor.WriteSeqj("boss.seqj", "Idle");
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        var panel = editor.Control<AnimationEditor.Views.Controls.ProjectPanelControl>("ProjectPanel");
        panel.TreeRoots.Select(node => node.Name).ShouldBe(new[] { "boss.seqj", "hero.achx" }, ignoreOrder: true);
        panel.TreeRoots.Single(node => node.Name == "boss.seqj").ShowSequenceIcon.ShouldBeTrue();

        editor.DoubleClickAt(editor.CenterOf(PanelsScenarioTests.RowFor(editor, panel, "boss.seqj")));
        editor.Wait(TimeSpan.FromMilliseconds(200));

        SequencePaneShown(editor).ShouldBeTrue();
        editor.Tabs.ActiveTab!.IsPreview.ShouldBeFalse();
        editor.SequenceNames.ShouldBe(new[] { "Idle" });
    }

    [AvaloniaFact]
    public void RestoringASessionWhoseActiveTabIsASequence_ShowsIt()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        string boss = Path.Combine(folder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(boss);
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new List<string> { boss },
            ActiveTabPath = boss,
        }));

        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);
            editor.WaitUntil(() => editor.TabLabels.Count == 1, TimeSpan.FromSeconds(5));
            editor.Layout();

            SequencePaneShown(editor).ShouldBeTrue();
            editor.SequenceNames.ShouldBe(new[] { "Idle" });
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }
}
