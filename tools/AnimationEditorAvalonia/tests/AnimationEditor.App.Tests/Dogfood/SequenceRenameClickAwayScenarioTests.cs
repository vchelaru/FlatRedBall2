using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A sequence rename box that loses focus commits the rename but never moves the selection: clicking another row
/// leaves that row selected (list, Inspector, timeline and preview), and clicking the timeline, the library or the
/// Inspector leaves the renamed row selected.
/// </summary>
public class SequenceRenameClickAwayScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static void ClickRow(AnimationEditorHarness editor, int row) => editor.Click((Control)List(editor).ContainerFromIndex(row)!);

    private static InlineRenameBox BoxOf(AnimationEditorHarness editor, int row) =>
        List(editor).ContainerFromIndex(row)!.GetVisualDescendants().OfType<InlineRenameBox>().Single();

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    // Idle and Mid are empty; Attack (the last row) has a track with a chain, so it alone has a length.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Sequences.Add(new SequenceSave { Name = "Mid" });
        var attack = new SequenceSave { Name = "Attack" };
        var body = new SequenceTrackSave { Name = "Body" };
        body.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
        attack.Tracks.Add(body);
        save.Sequences.Add(attack);
        save.Save(path);
        return path;
    }

    /// <summary>The list selection, the Inspector and the timeline must all agree on the same sequence.</summary>
    private static void ShouldShowSequence(AnimationEditorHarness editor, int index, string name)
    {
        List(editor).SelectedIndex.ShouldBe(index);
        Inspector(editor).SequencePanel.IsVisible.ShouldBeTrue();
        Inspector(editor).SequenceNameBox.Text.ShouldBe(name);
        // Only Attack has content, so the timeline follows the selection only if its length says so.
        (Timeline(editor).SequenceLength > 0).ShouldBe(name == "Attack");
    }

    private static void BeginRenameByDoubleTap(AnimationEditorHarness editor, int row, string newName)
    {
        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(row)!));
        var box = BoxOf(editor, row);
        box.IsEditing.ShouldBeTrue();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(newName);
    }

    [AvaloniaFact]
    public async Task ClickingAnotherRow_WhileRenamingByDoubleTap_CommitsTheRename_AndSelectsTheClickedRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginRenameByDoubleTap(editor, 0, "Zero");
        ClickRow(editor, 2);

        SavedNames(boss).ShouldBe(new[] { "Zero", "Mid", "Attack" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Zero'" });
        ShouldShowSequence(editor, 2, "Attack");
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task ClickingAnotherRow_WhileRenamingANewSequence_CommitsTheRename_AndSelectsTheClickedRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        editor.Click(editor.Control<Button>("AddSequenceButton"));
        await editor.WaitUntilAsync(() => BoxOf(editor, 3).IsEditing, TimeSpan.FromSeconds(1));
        editor.Type("Fresh");
        ClickRow(editor, 0);

        SavedNames(boss).ShouldBe(new[] { "Idle", "Mid", "Attack", "Fresh" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'", "Rename Sequence 'Sequence' to 'Fresh'" });
        ShouldShowSequence(editor, 0, "Idle");
    }

    [AvaloniaFact]
    public async Task ClickingAnotherRow_WhileRenamingToATakenName_ShowsTheBanner_KeepsTheName_AndSelectsTheClickedRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginRenameByDoubleTap(editor, 0, "Mid");
        ClickRow(editor, 2);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("already exists");
        SavedNames(boss).ShouldBe(new[] { "Idle", "Mid", "Attack" });
        editor.UndoLabels.ShouldBeEmpty();
        ShouldShowSequence(editor, 2, "Attack");
    }

    [AvaloniaFact]
    public async Task ClickingAnotherRow_WhileRenamingToAnEmptyName_ShowsTheBanner_KeepsTheName_AndSelectsTheClickedRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginRenameByDoubleTap(editor, 0, "  ");
        ClickRow(editor, 2);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("needs a name");
        SavedNames(boss).ShouldBe(new[] { "Idle", "Mid", "Attack" });
        ShouldShowSequence(editor, 2, "Attack");
    }

    [AvaloniaFact]
    public async Task UndoAfterAClickAwayRename_RestoresTheName_AndSelectsTheRenamedRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        BeginRenameByDoubleTap(editor, 0, "Zero");
        ClickRow(editor, 2);

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.SequenceNames.ShouldBe(new[] { "Idle", "Mid", "Attack" });
        ShouldShowSequence(editor, 0, "Idle");
        editor.Press(Key.Y, RawInputModifiers.Control);
        ShouldShowSequence(editor, 0, "Zero");
    }

    [AvaloniaFact]
    public async Task EnterKeepsTheRenamedRowSelected_AndEscapeKeepsTheCurrentSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginRenameByDoubleTap(editor, 0, "Zero");
        editor.Press(Key.Enter);
        SavedNames(boss).ShouldBe(new[] { "Zero", "Mid", "Attack" });
        ShouldShowSequence(editor, 0, "Zero");

        ClickRow(editor, 2);
        BeginRenameByDoubleTap(editor, 2, "Nope");
        editor.Press(Key.Escape);
        SavedNames(boss).ShouldBe(new[] { "Zero", "Mid", "Attack" });
        ShouldShowSequence(editor, 2, "Attack");
    }

    [AvaloniaFact]
    public async Task ClickingTheTimelineOrTheInspector_WhileRenaming_CommitsAndKeepsTheRenamedRowSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);

        BeginRenameByDoubleTap(editor, 0, "A1");
        editor.ClickAt(editor.PointIn(Timeline(editor), 300, 200));
        SavedNames(boss)[0].ShouldBe("A1");
        ShouldShowSequence(editor, 0, "A1");

        ClickRow(editor, 1);
        BeginRenameByDoubleTap(editor, 1, "B1");
        editor.Click(Inspector(editor).SequenceNameBox);
        SavedNames(boss)[1].ShouldBe("B1");
        ShouldShowSequence(editor, 1, "B1");
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'A1'", "Rename Sequence 'Mid' to 'B1'" });
    }
}
