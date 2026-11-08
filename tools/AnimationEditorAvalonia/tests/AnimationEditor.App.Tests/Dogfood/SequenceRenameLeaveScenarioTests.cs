using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// An in-place rename (a sequence row or a track header) that is still open when the user does anything else commits
/// the typed name, the way a click-away on the list or timeline does. Escape still cancels, and a refused name behaves
/// as it does on a click-away: the banner, the old name.
/// </summary>
public class SequenceRenameLeaveScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static SequenceLibraryControl Library(AnimationEditorHarness editor) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary");

    // Attack (row 0) has the tracks Body, with a Walk chain, and Arms; Idle (row 1) is empty. boss.seqj also has
    // hero.achx as a source, so the library shows chips.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var attack = new SequenceSave { Name = "Attack" };
        var body = new SequenceTrackSave { Name = "Body" };
        body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        attack.Tracks.Add(body);
        attack.Tracks.Add(new SequenceTrackSave { Name = "Arms" });
        save.Sequences.Add(attack);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(path);
        return path;
    }

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static List<string> SavedTracks(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    private static Point OnHeader(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(lane) + 10);

    private static void BeginSequenceRename(AnimationEditorHarness editor, int row, string newName)
    {
        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(row)!));
        var box = List(editor).ContainerFromIndex(row)!.GetVisualDescendants().OfType<InlineRenameBox>().Single();
        box.IsEditing.ShouldBeTrue();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(newName);
    }

    private static void BeginTrackRename(AnimationEditorHarness editor, int lane, string newName)
    {
        editor.DoubleClickAt(OnHeader(editor, lane));
        var box = Timeline(editor).GetVisualChildren().OfType<InlineRenameBox>().Single();
        box.IsEditing.ShouldBeTrue();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(newName);
    }

    private static void MouseButtonClick(AnimationEditorHarness editor, MouseButton button)
    {
        var point = new Point(300, 300);
        editor.Window.MouseDown(point, button);
        editor.Window.MouseUp(point, button);
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task SwitchingTabs_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        string other = editor.WriteSeqj("other.seqj", "One", "Two");
        await editor.OpenAsync(boss);
        await editor.OpenAsync(other);
        editor.ClickTab("boss.seqj");

        BeginSequenceRename(editor, 1, "Rest");
        editor.ClickTab("other.seqj");

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
        editor.ErrorBannerText.ShouldBeNull();
        editor.ClickTab("boss.seqj");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Rest" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Rest'" });
        List(editor).SelectedIndex.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task SwitchingTabs_WhileRenamingATrack_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        string other = editor.WriteSeqj("other.seqj", "One", "Two");
        await editor.OpenAsync(boss);
        await editor.OpenAsync(other);
        editor.ClickTab("boss.seqj");

        BeginTrackRename(editor, 1, "Legs");
        editor.ClickTab("other.seqj");

        SavedTracks(boss).ShouldBe(new[] { "Body", "Legs" });
        editor.ClickTab("boss.seqj");
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Arms' to 'Legs'" });
    }

    [AvaloniaFact]
    public async Task SwitchingToAnAchxTab_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.ClickTab("boss.seqj");

        BeginSequenceRename(editor, 1, "Rest");
        editor.ClickTab("hero.achx");

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
    }

    [AvaloniaFact]
    public async Task OpeningAnotherFile_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        string other = editor.WriteSeqj("other.seqj", "One", "Two");
        await editor.OpenAsync(boss);

        BeginSequenceRename(editor, 1, "Rest");
        await editor.OpenAsync(other);

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
    }

    [AvaloniaFact]
    public async Task ClickingALibraryChip_WhileRenamingASequence_CommitsTheRename_AndKeepsTheSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginSequenceRename(editor, 1, "Rest");
        editor.Click(Library(editor).ChipFor("hero.achx", "Run")!);

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Rest'" });
        List(editor).SelectedIndex.ShouldBe(1);
        editor.ErrorBannerText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task ClickingALibrarySourceGroup_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginSequenceRename(editor, 1, "Rest");
        editor.Click(Library(editor).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "hero.achx"));

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
        Library(editor).IsCollapsed("hero.achx").ShouldBeTrue();
        List(editor).SelectedIndex.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task ClickingALibraryChip_WhileRenamingATrack_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginTrackRename(editor, 1, "Legs");
        editor.Click(Library(editor).ChipFor("hero.achx", "Run")!);

        SavedTracks(boss).ShouldBe(new[] { "Body", "Legs" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Track 'Arms' to 'Legs'" });
    }

    [AvaloniaFact]
    public async Task ClickingAnotherSequence_WhileRenamingATrack_CommitsTheRename_AndSelectsTheClickedRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginTrackRename(editor, 1, "Legs");
        editor.Click((Control)List(editor).ContainerFromIndex(1)!);

        SavedTracks(boss).ShouldBe(new[] { "Body", "Legs" });
        List(editor).SelectedIndex.ShouldBe(1);
        editor.Control<SequenceInspectorControl>("SequenceInspector").SequenceNameBox.Text.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task ClosingTheTab_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        string other = editor.WriteSeqj("other.seqj", "One", "Two");
        await editor.OpenAsync(other);
        await editor.OpenAsync(boss);

        BeginSequenceRename(editor, 1, "Rest");
        editor.CloseTab("boss.seqj");

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginSequenceRename(editor, 1, "Rest");
        editor.Window.Close();

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
    }

    [AvaloniaFact]
    public async Task ClosingTheWindow_WhileRenamingATrack_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginTrackRename(editor, 1, "Legs");
        editor.Window.Close();

        SavedTracks(boss).ShouldBe(new[] { "Body", "Legs" });
    }

    [AvaloniaFact]
    public async Task CtrlZ_WhileRenaming_CommitsFirst_SoUndoTakesTheRenameBack_AndCtrlYBringsItAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        BeginSequenceRename(editor, 0, "Strike");
        editor.Press(Key.Enter);
        BeginSequenceRename(editor, 1, "Rest");

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.SequenceNames.ShouldBe(new[] { "Strike", "Idle" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Attack' to 'Strike'" });
        editor.Press(Key.Y, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Strike", "Rest" });
    }

    [AvaloniaFact]
    public async Task EditUndoFromTheMenu_WhileRenaming_CommitsFirst()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        BeginSequenceRename(editor, 0, "Strike");
        editor.Press(Key.Enter);
        BeginSequenceRename(editor, 1, "Rest");

        editor.ClickMenu("MenuUndo");

        editor.SequenceNames.ShouldBe(new[] { "Strike", "Idle" });
        editor.ClickMenu("MenuRedo");
        editor.SequenceNames.ShouldBe(new[] { "Strike", "Rest" });
    }

    [AvaloniaFact]
    public async Task CtrlS_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginSequenceRename(editor, 1, "Rest");
        editor.Press(Key.S, RawInputModifiers.Control);

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
    }

    [AvaloniaFact]
    public async Task MouseBack_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.Click((Control)List(editor).ContainerFromIndex(1)!);
        BeginSequenceRename(editor, 1, "Rest");

        MouseButtonClick(editor, MouseButton.XButton1);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
        editor.ErrorBannerText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task AltLeft_WhileRenamingASequence_CommitsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.Click((Control)List(editor).ContainerFromIndex(1)!);
        BeginSequenceRename(editor, 1, "Rest");

        editor.Press(Key.Left, RawInputModifiers.Alt);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        SavedNames(boss).ShouldBe(new[] { "Attack", "Rest" });
    }

    [AvaloniaFact]
    public async Task ARefusedName_OnLeavingByTabSwitch_ShowsTheBanner_AndKeepsTheOldName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        string other = editor.WriteSeqj("other.seqj", "One", "Two");
        await editor.OpenAsync(boss);
        await editor.OpenAsync(other);
        editor.ClickTab("boss.seqj");

        BeginSequenceRename(editor, 1, "Attack");
        editor.ClickTab("other.seqj");

        editor.ErrorBannerText.ShouldNotBeNull().ShouldContain("already exists");
        SavedNames(boss).ShouldBe(new[] { "Attack", "Idle" });
        editor.ClickTab("boss.seqj");
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Escape_StillCancels_ThenLeavingChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        string other = editor.WriteSeqj("other.seqj", "One", "Two");
        await editor.OpenAsync(boss);
        await editor.OpenAsync(other);
        editor.ClickTab("boss.seqj");

        BeginSequenceRename(editor, 1, "Rest");
        editor.Press(Key.Escape);
        editor.ClickTab("other.seqj");
        editor.ClickTab("boss.seqj");

        SavedNames(boss).ShouldBe(new[] { "Attack", "Idle" });
        editor.UndoLabels.ShouldBeEmpty();
    }
}
