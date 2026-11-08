using AnimationEditor.App.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The sequence list and track header menus offer Move To Top / Up / Down / To Bottom and (on the list) Sort Sequences
/// Alphabetically, in the order, wording and icons of the ANIMATIONS tree's menu (impossible moves are left out, as the tree
/// does), and each sequence row ends in the tree's grey "3 fr · 0.30s"-style summary ("3 tracks · 2.20s"). Popups are not in the
/// headless back-buffer, so menus are checked by their items. Fixture for the summary and track moves: Attack (tracks Body, Head,
/// Tail; 2.2 s long, see <see cref="SequenceTimelineMultiSelectScenarioTests"/>), then the empty sequences Idle and Walk.
/// </summary>
public class SequenceMoveSortSummaryScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static List<string> Headers(ContextMenu menu) =>
        menu.Items.Select(item => item is MenuItem menuItem ? (string)menuItem.Header! : "-").ToList();

    private static string[] RowTexts(AnimationEditorHarness editor, int row) =>
        ((Control)List(editor).ContainerFromIndex(row)!).GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? "").ToArray();

    private static List<string> SelectedNames(AnimationEditorHarness editor) =>
        List(editor).SelectedItems!.OfType<AnimationEditor.App.Models.SequenceListItem>().Select(row => row.Name).ToList();

    private static void ClickRow(AnimationEditorHarness editor, int row, RawInputModifiers modifiers = RawInputModifiers.None) =>
        editor.ClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(row)!), modifiers);

    private static void RightClickRow(AnimationEditorHarness editor, int row) =>
        editor.RightClick((Control)List(editor).ContainerFromIndex(row)!);

    private static void RightClickBlankList(AnimationEditorHarness editor)
    {
        ListBox list = List(editor);
        editor.RightClickAt(editor.PointIn(list, 20, list.Bounds.Height - 10));
    }

    private static Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name);

    private static string WriteThreeSequences(AnimationEditorHarness editor)
    {
        string path = WriteFixture(editor);
        var save = SequenceListSave.FromFile(path);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Sequences.Add(new SequenceSave { Name = "Walk" });
        save.Save(path);
        return path;
    }

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static List<string> SavedTrackNames(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    private static readonly string[] MoveGroup = { "Move To Top", "Move Up", "Move Down", "Move To Bottom" };

    private static readonly string[] RestOfRowMenu =
    {
        "Add Sequence", "-", "Copy", "Cut", "Paste", "Duplicate", "-", "Rename…", "Copy Qualified Name", "-", "Delete Sequence", "-", "Sort Sequences Alphabetically",
    };

    // ── Row summary ─────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task EachRow_EndsInItsTrackCountAndLength_LikeAnAnimationRow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        RowTexts(editor, 0).ShouldBe(new[] { "Attack", "3 tracks · 2.20s" });
        RowTexts(editor, 1).ShouldBe(new[] { "Idle", "0 tracks · 0.00s" });
        RowTexts(editor, 2).ShouldBe(new[] { "Walk", "0 tracks · 0.00s" });
    }

    [AvaloniaFact]
    public async Task TheSummary_FollowsTracksAndChains_LiveAndThroughUndo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        // Body's last chain (Run, 2 to 2.2 s) goes: the sequence now ends where Head's Walk does.
        editor.RightClickAt(At(editor, 0, 2.1));
        editor.PickMenuItem(Timeline(editor).Menu, "Remove from Track");
        RowTexts(editor, 0)[1].ShouldBe("3 tracks · 2.00s");
        editor.Press(Key.Z, RawInputModifiers.Control);
        RowTexts(editor, 0)[1].ShouldBe("3 tracks · 2.20s");

        // A new track (the blank menu's Add Track) is counted.
        editor.RightClickAt(At(editor, 2, 5.0));
        editor.PickMenuItem(Timeline(editor).Menu, "Add Track");
        RowTexts(editor, 0)[1].ShouldBe("4 tracks · 2.20s");
    }

    [AvaloniaFact]
    public async Task ASequenceWithOneTrack_SaysTrackInTheSingular()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        var save = SequenceListSave.FromFile(path);
        save.Sequences[1].Tracks.Add(Track("Solo", ("Run", 0, 1)));
        save.Save(path);
        await editor.OpenAsync(path);

        RowTexts(editor, 1).ShouldBe(new[] { "Idle", "1 track · 0.20s" });
    }

    [AvaloniaFact]
    public async Task TheSummary_IsTheSameWithLoopOnOrOff()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        string before = RowTexts(editor, 0)[1];

        editor.Click(editor.Control<ToggleButton>("SequenceLoopToggle"));

        RowTexts(editor, 0)[1].ShouldBe(before);
    }

    // ── Row menu: Move and Sort ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task ARowInTheMiddle_OffersEveryMove_FirstThenTheRestOfTheMenu_EndingInSort()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        RightClickRow(editor, 1);

        var menu = List(editor).ContextMenu!;
        Headers(menu).ShouldBe(MoveGroup.Append("-").Concat(RestOfRowMenu).ToArray());
        foreach (var item in menu.Items.OfType<MenuItem>())
            item.Icon.ShouldNotBeNull($"\"{item.Header}\" has no icon");
    }

    [AvaloniaFact]
    public async Task TheFirstRow_LeavesOutMoveUpAndToTop_TheLastLeavesOutMoveDownAndToBottom()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        RightClickRow(editor, 0);
        Headers(List(editor).ContextMenu!).ShouldBe(new[] { "Move Down", "Move To Bottom", "-" }.Concat(RestOfRowMenu).ToArray());
        List(editor).ContextMenu!.Close();

        RightClickRow(editor, 2);
        Headers(List(editor).ContextMenu!).ShouldBe(new[] { "Move To Top", "Move Up", "-" }.Concat(RestOfRowMenu).ToArray());
    }

    [AvaloniaFact]
    public async Task AnOnlySequence_HasNoMoveItems_OrDivider()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle"));

        RightClickRow(editor, 0);

        Headers(List(editor).ContextMenu!).ShouldBe(RestOfRowMenu);
    }

    [AvaloniaTheory]
    [InlineData("Move To Top", new[] { "C", "A", "B", "D", "E" })]
    [InlineData("Move Up", new[] { "A", "C", "B", "D", "E" })]
    [InlineData("Move Down", new[] { "A", "B", "D", "C", "E" })]
    [InlineData("Move To Bottom", new[] { "A", "B", "D", "E", "C" })]
    public async Task AMoveItem_MovesTheSequence_AsOneUndoEntry_AndUndoPutsItBack(string item, string[] expected)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = editor.WriteSeqj("boss.seqj", "A", "B", "C", "D", "E");
        await editor.OpenAsync(path);
        RightClickRow(editor, 2);

        editor.PickMenuItem(List(editor).ContextMenu!, item);

        editor.SequenceNames.ShouldBe(expected);
        SavedNames(path).ShouldBe(expected);
        editor.UndoLabels.Count.ShouldBe(1);
        editor.UndoLabels[0].ShouldContain("'C'");
        SelectedNames(editor).ShouldBe(new[] { "C" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "A", "B", "C", "D", "E" });
    }

    [AvaloniaFact]
    public async Task SeveralSelected_MoveAsOneBlock_AndStaySelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = editor.WriteSeqj("boss.seqj", "A", "B", "C", "D", "E");
        await editor.OpenAsync(path);
        ClickRow(editor, 1);
        ClickRow(editor, 2, RawInputModifiers.Control);
        RightClickRow(editor, 2);

        editor.PickMenuItem(List(editor).ContextMenu!, "Move To Bottom");

        editor.SequenceNames.ShouldBe(new[] { "A", "D", "E", "B", "C" });
        editor.UndoLabels.Count.ShouldBe(1);
        editor.UndoLabels[0].ShouldContain("2");
        SelectedNames(editor).ShouldBe(new[] { "B", "C" });
    }

    [AvaloniaFact]
    public async Task Sort_FromTheBlankMenu_OrdersByName_KeepsTheSelection_AsOneUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = editor.WriteSeqj("boss.seqj", "walk", "Attack", "idle", "Boss");
        await editor.OpenAsync(path);
        ClickRow(editor, 2);
        RightClickBlankList(editor);
        Headers(List(editor).ContextMenu!).ShouldBe(new[] { "Add Sequence", "-", "Paste", "-", "Sort Sequences Alphabetically" });

        editor.PickMenuItem(List(editor).ContextMenu!, "Sort Sequences Alphabetically");

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Boss", "idle", "walk" });
        SavedNames(path).ShouldBe(new[] { "Attack", "Boss", "idle", "walk" });
        editor.UndoLabels.ShouldBe(new[] { "Sort Sequences Alphabetically" });
        SelectedNames(editor).ShouldBe(new[] { "idle" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "walk", "Attack", "idle", "Boss" });
        SelectedNames(editor).ShouldBe(new[] { "idle" });
    }

    [AvaloniaFact]
    public async Task Sort_AlreadySorted_ChangesNothing_AndMakesNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "A", "B", "C"));
        RightClickRow(editor, 1);

        editor.PickMenuItem(List(editor).ContextMenu!, "Sort Sequences Alphabetically");

        editor.SequenceNames.ShouldBe(new[] { "A", "B", "C" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    // ── Track header menu ───────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task ATrackHeaderMenu_OffersTheMovesFirst_OnlyTheOnesThatCanHappen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        string[] rest = { "Copy", "Cut", "Paste", "Duplicate", "-", "Rename Track…", "Hide Track", "-", "Delete Track" };

        editor.RightClickAt(Header(editor, 1));
        Headers(Timeline(editor).Menu).ShouldBe(MoveGroup.Append("-").Concat(rest).ToArray());
        foreach (var item in Timeline(editor).Menu.Items.OfType<MenuItem>().Where(item => ((string)item.Header!).StartsWith("Move")))
            item.Icon.ShouldNotBeNull($"\"{item.Header}\" has no icon");
        Timeline(editor).Menu.Close();

        editor.RightClickAt(Header(editor, 0));
        Headers(Timeline(editor).Menu).ShouldBe(new[] { "Move Down", "Move To Bottom", "-" }.Concat(rest).ToArray());
        Timeline(editor).Menu.Close();

        editor.RightClickAt(Header(editor, 2));
        Headers(Timeline(editor).Menu).ShouldBe(new[] { "Move To Top", "Move Up", "-" }.Concat(rest).ToArray());
    }

    [AvaloniaTheory]
    [InlineData(1, "Move To Top", new[] { "Head", "Body", "Tail" })]
    [InlineData(1, "Move Up", new[] { "Head", "Body", "Tail" })]
    [InlineData(1, "Move Down", new[] { "Body", "Tail", "Head" })]
    [InlineData(0, "Move To Bottom", new[] { "Head", "Tail", "Body" })]
    public async Task ATrackMoveItem_MovesTheTrack_TowardTheFrontLaneForUp_AsOneUndoEntry(int lane, string item, string[] expected)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.RightClickAt(Header(editor, lane));

        editor.PickMenuItem(Timeline(editor).Menu, item);

        SavedTrackNames(path).ShouldBe(expected);
        editor.UndoLabels.Count.ShouldBe(1);
        editor.UndoLabels[0].ShouldContain("Move Track");
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedTrackNames(path).ShouldBe(new[] { "Body", "Head", "Tail" });
    }

    [AvaloniaFact]
    public async Task ATrackHeaderMenu_WithSeveralTracksSelected_OffersNoMoves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);

        editor.RightClickAt(Header(editor, 1));

        Headers(Timeline(editor).Menu).ShouldNotContain("Move Up");
        Headers(Timeline(editor).Menu).First().ShouldBe("Copy 2 Tracks");
    }
}
