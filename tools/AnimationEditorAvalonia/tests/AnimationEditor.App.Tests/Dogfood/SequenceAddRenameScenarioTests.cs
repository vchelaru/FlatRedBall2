using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Add Sequence (the button, the blank-space menu item and the row menu item) opens the new row's name for editing in
/// place with the default selected, like "+ new track" does for a track. Enter commits as its own undo entry, Escape
/// keeps the default, a refused name shows the banner and keeps the default.
/// </summary>
public class SequenceAddRenameScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static InlineRenameBox? BoxOf(AnimationEditorHarness editor, int row) =>
        List(editor).ContainerFromIndex(row)?.GetVisualDescendants().OfType<InlineRenameBox>().SingleOrDefault();

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static string PlayIconPath(AnimationEditorHarness editor) =>
        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!;

    private static void AddFromButton(AnimationEditorHarness editor) =>
        editor.Click(editor.Control<Button>("AddSequenceButton"));

    private static void AddFromBlankMenu(AnimationEditorHarness editor)
    {
        ListBox list = List(editor);
        editor.RightClickAt(editor.PointIn(list, 20, list.Bounds.Height - 10));
        editor.PickMenuItem(list.ContextMenu!, "Add Sequence");
    }

    private static void AddFromRowMenu(AnimationEditorHarness editor)
    {
        editor.RightClick((Control)List(editor).ContainerFromIndex(0)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Add Sequence");
    }

    /// <summary>Waits for the new last row's name box to open, and returns it.</summary>
    private static async Task<InlineRenameBox> NewRowBoxAsync(AnimationEditorHarness editor)
    {
        int row = editor.SequenceNames.Count - 1;
        await editor.WaitUntilAsync(() => BoxOf(editor, row) is { IsEditing: true }, TimeSpan.FromSeconds(1));
        BoxOf(editor, row).ShouldNotBeNull().IsEditing.ShouldBeTrue("the new row's name should be open for editing");
        return BoxOf(editor, row)!;
    }

    private static async Task AssertOpensForEditing(Action<AnimationEditorHarness> add)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle", "Attack"));

        add(editor);

        var box = await NewRowBoxAsync(editor);
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Attack", "Sequence" });
        box.IsVisible.ShouldBeTrue();
        box.Text.ShouldBe("Sequence");
        box.SelectedText.ShouldBe("Sequence");
        box.IsFocused.ShouldBeTrue();
        List(editor).SelectedIndex.ShouldBe(2);
    }

    [AvaloniaFact]
    public Task AddSequenceButton_OpensTheNewNameForEditing_WithTheDefaultSelected() =>
        AssertOpensForEditing(AddFromButton);

    [AvaloniaFact]
    public Task BlankSpaceMenuAddSequence_OpensTheNewNameForEditing_WithTheDefaultSelected() =>
        AssertOpensForEditing(AddFromBlankMenu);

    [AvaloniaFact]
    public Task RowMenuAddSequence_OpensTheNewNameForEditing_WithTheDefaultSelected() =>
        AssertOpensForEditing(AddFromRowMenu);

    [AvaloniaFact]
    public async Task TypingThenEnter_RenamesAsItsOwnUndoStep_AndUndoRestoresTheDefaultThenRemovesTheSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);

        AddFromButton(editor);
        await NewRowBoxAsync(editor);
        editor.Type("Charge");
        editor.Press(Key.Enter);

        BoxOf(editor, 1)!.IsVisible.ShouldBeFalse();
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Charge" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Charge" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'", "Rename Sequence 'Sequence' to 'Charge'" });
        List(editor).SelectedIndex.ShouldBe(1);
        editor.ProblemText.ShouldBeNull();

        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Sequence" });
        List(editor).SelectedIndex.ShouldBe(1);
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.SequenceNames.ShouldBe(new[] { "Idle" });
        List(editor).SelectedIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task Escape_KeepsTheDefaultName_WithNoExtraUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);

        AddFromButton(editor);
        var box = await NewRowBoxAsync(editor);
        editor.Type("Nope");
        editor.Press(Key.Escape);

        box.IsVisible.ShouldBeFalse();
        box.IsEditing.ShouldBeFalse();
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Sequence" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Sequence" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task ATakenName_ShowsTheBanner_AndKeepsTheDefault()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);

        AddFromButton(editor);
        await NewRowBoxAsync(editor);
        editor.Type("Idle");
        editor.Press(Key.Enter);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("already exists");
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Sequence" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Sequence" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
    }

    [AvaloniaFact]
    public async Task AnEmptyName_ShowsTheBanner_AndKeepsTheDefault()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);

        AddFromButton(editor);
        var box = await NewRowBoxAsync(editor);
        box.Text = "  ";
        editor.Press(Key.Enter);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("needs a name");
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Sequence" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
    }

    [AvaloniaFact]
    public async Task ClickingAway_CommitsTheTypedName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);

        AddFromButton(editor);
        var box = await NewRowBoxAsync(editor);
        editor.Type("Charge");
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        editor.ClickAt(editor.PointIn(timeline, 400, timeline.Viewport.LaneTop(3)));

        box.IsEditing.ShouldBeFalse();
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Charge" });
        SavedNames(boss).ShouldBe(new[] { "Idle", "Charge" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'", "Rename Sequence 'Sequence' to 'Charge'" });
    }

    [AvaloniaFact]
    public async Task DeleteAndSpace_InTheBox_EditTheTextOnly_NeverDeleteTheSequenceOrTogglePlayback()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(boss);
        string playIcon = PlayIconPath(editor);

        AddFromButton(editor);
        var box = await NewRowBoxAsync(editor);
        editor.Type("a");
        editor.Press(Key.Space);
        editor.Type(" b");
        editor.Press(Key.Left);
        editor.Press(Key.Delete);

        box.IsEditing.ShouldBeTrue();
        box.Text.ShouldBe("a ");
        editor.SequenceNames.ShouldBe(new[] { "Idle", "Sequence" });
        editor.UndoLabels.ShouldBe(new[] { "Add Sequence 'Sequence'" });
        PlayIconPath(editor).ShouldBe(playIcon);
    }

    [AvaloniaFact]
    public async Task AddingPastTheVisibleRows_ScrollsTheNewRowIntoView_BeforeTheBoxOpens()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", Enumerable.Range(1, 40).Select(i => $"Seq{i}").ToArray()));
        ListBox list = List(editor);

        AddFromButton(editor);
        var box = await NewRowBoxAsync(editor);

        var top = box.TranslatePoint(new Point(0, 0), list).ShouldNotBeNull();
        top.Y.ShouldBeGreaterThanOrEqualTo(0);
        (top.Y + box.Bounds.Height).ShouldBeLessThanOrEqualTo(list.Bounds.Height);
        box.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task TheNewSequence_IsSelected_TheInspectorShowsItsSection_AndThePreviewIsAtTimeZero()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle"));

        AddFromButton(editor);
        await NewRowBoxAsync(editor);

        List(editor).SelectedIndex.ShouldBe(1);
        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");
        inspector.SequencePanel.IsVisible.ShouldBeTrue();
        inspector.SequenceNameBox.Text.ShouldBe("Sequence");
        editor.Control<SequenceTimelineControl>("SequenceTimeline").Playhead.ShouldBe(0);
        editor.Control<TextBlock>("SequenceTimeText").Text.ShouldBe("0.00 s / 0.00 s");
    }
}
