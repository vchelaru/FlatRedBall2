using AnimationEditor.App.Controls;
using AnimationEditor.App.Models;
using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Keyboard focus and the edit hotkeys on a .seqj tab. Rule: the last list-or-timeline pane the user touched owns Copy, Cut,
/// Duplicate, Delete and the Edit menu, wherever the focus sits now (Play button, library, preview, inspector); Paste follows
/// the clipboard. An inline rename hands the focus back to its pane. Fixture: Attack (tracks Body: Walk, Walk, Run; Head:
/// Run, Walk; Tail: empty), then the empty sequences Idle and Walk.
/// </summary>
public class SequenceFocusRoutingScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static string WriteThreeSequences(AnimationEditorHarness editor)
    {
        string path = WriteFixture(editor);
        var save = SequenceListSave.FromFile(path);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Sequences.Add(new SequenceSave { Name = "Walk" });
        save.Save(path);
        return path;
    }

    private static Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name);

    private static IInputElement? Focused(AnimationEditorHarness editor) => editor.Window.FocusManager!.GetFocusedElement();

    private static bool Within(Visual parent, IInputElement? focused) =>
        focused is Visual visual && (visual == parent || parent.IsVisualAncestorOf(visual));

    private static bool FocusIsInList(AnimationEditorHarness editor) => Within(List(editor), Focused(editor));

    private static bool FocusIsInTimeline(AnimationEditorHarness editor) => Within(Timeline(editor), Focused(editor));

    private static string Describe(IInputElement? focused) => focused?.GetType().Name ?? "nothing";

    private static async Task SettleAsync(AnimationEditorHarness editor) => await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

    private static async Task PressAsync(AnimationEditorHarness editor, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        editor.Press(key, modifiers);
        await SettleAsync(editor);
    }

    private static InlineRenameBox? BoxOf(AnimationEditorHarness editor, int row) =>
        List(editor).ContainerFromIndex(row)?.GetVisualDescendants().OfType<InlineRenameBox>().SingleOrDefault();

    private static void ClickRow(AnimationEditorHarness editor, int row, RawInputModifiers modifiers = RawInputModifiers.None) =>
        editor.ClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(row)!), modifiers);

    private static List<string> SavedSequences(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static int SavedChainCount(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Sum(track => track.Chains.Count);

    private static List<string> SavedTrackNames(string path) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(track => track.Name).ToList();

    private static async Task<string?> ClipboardAsync(AnimationEditorHarness editor) => await editor.Window.Clipboard!.TryGetTextAsync();

    private static async Task BeginAddSequenceAsync(AnimationEditorHarness editor)
    {
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        int row = editor.SequenceNames.Count - 1;
        (await editor.WaitUntilAsync(() => BoxOf(editor, row) is { IsEditing: true }, TimeSpan.FromSeconds(1))).ShouldBeTrue();
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public async Task AddSequence_ThenEnterOrEscape_PutsTheFocusOnTheListRow_SoDeleteRemovesThatSequence(Key key)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        await BeginAddSequenceAsync(editor);

        await PressAsync(editor, key);

        FocusIsInList(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
        await PressAsync(editor, Key.Delete);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle", "Walk" });
        SavedChainCount(path).ShouldBe(5);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public async Task AddSequence_ThenEnterOrEscape_CtrlD_DuplicatesTheSequence(Key key)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        await BeginAddSequenceAsync(editor);
        await PressAsync(editor, key);

        await PressAsync(editor, Key.D, RawInputModifiers.Control);

        editor.SequenceNames.Count.ShouldBe(5);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public async Task AddSequence_ThenEnterOrEscape_CtrlC_CtrlV_PastesACopy(Key key)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        await BeginAddSequenceAsync(editor);
        await PressAsync(editor, key);

        await PressAsync(editor, Key.C, RawInputModifiers.Control);
        await PressAsync(editor, Key.V, RawInputModifiers.Control);

        editor.SequenceNames.Count.ShouldBe(5);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public async Task RenamingARowByDoubleClick_ThenEnterOrEscape_PutsTheFocusOnTheListRow(Key key)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(1)!));
        (await editor.WaitUntilAsync(() => BoxOf(editor, 1) is { IsEditing: true }, TimeSpan.FromSeconds(1))).ShouldBeTrue();

        await PressAsync(editor, key);

        FocusIsInList(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
        await PressAsync(editor, Key.Delete);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Walk" });
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public async Task RenamingATrackByDoubleClick_ThenEnterOrEscape_PutsTheFocusOnTheTimeline_SoCtrlDDuplicatesTheTrack(Key key)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.DoubleClickAt(Header(editor, 1));
        Timeline(editor).IsRenamingTrack.ShouldBeTrue();

        await PressAsync(editor, key);

        FocusIsInTimeline(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
        await PressAsync(editor, Key.D, RawInputModifiers.Control);
        SavedTrackNames(path).Count.ShouldBe(4);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    public async Task AddingATrackFromTheNewTrackCell_ThenEnterOrEscape_PutsTheFocusOnTheTimeline(Key key)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(3) + 10));
        Timeline(editor).IsRenamingTrack.ShouldBeTrue();

        await PressAsync(editor, key);

        FocusIsInTimeline(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
        await PressAsync(editor, Key.Delete);
        SavedTrackNames(path).Count.ShouldBe(3, "Delete removed the new track from the timeline");
    }

    [AvaloniaFact]
    public async Task DuplicatingSeveralSequences_LeavesTheFocusOnTheList()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 1);
        ClickRow(editor, 2, RawInputModifiers.Control);

        await PressAsync(editor, Key.D, RawInputModifiers.Control);

        editor.SequenceNames.Count.ShouldBe(5);
        FocusIsInList(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
    }

    [AvaloniaFact]
    public async Task DeletingASequenceFromItsMenu_LeavesTheFocusOnTheList()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        editor.RightClick((Control)List(editor).ContainerFromIndex(1)!);
        editor.PickMenuItem(List(editor).ContextMenu!, "Delete");
        await SettleAsync(editor);

        FocusIsInList(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
    }

    [AvaloniaFact]
    public async Task AfterTheListWasLastTouched_DeleteWithThePlayButtonFocused_RemovesTheSequence_NotTheTimelinesChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(At(editor, 0, 0.25));
        Timeline(editor).SelectedChains.Count.ShouldBe(1);
        ClickRow(editor, 1);
        editor.Click(editor.Control<Button>("SequencePlayButton"));

        await PressAsync(editor, Key.Delete);

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Walk" });
        SavedChainCount(path).ShouldBe(5);
    }

    [AvaloniaFact]
    public async Task AfterTheTimelineWasLastTouched_DeleteWithThePlayButtonFocused_RemovesTheChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        ClickRow(editor, 0);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.Click(editor.Control<Button>("SequencePlayButton"));

        await PressAsync(editor, Key.Delete);

        editor.SequenceNames.Count.ShouldBe(3);
        SavedChainCount(path).ShouldBe(4);
    }

    /// <summary>Every command acts on the last touched pane whether the focus sits on a toolbar button, the preview or an inspector control.</summary>
    [AvaloniaTheory]
    [InlineData("SequencePlayButton")]
    [InlineData("SequenceStopButton")]
    [InlineData("SequencePreview")]
    [InlineData("TrackFlipBox")]
    public async Task WithTheTimelineLastTouched_CopyPasteDuplicateAndDelete_WorkWhereverTheFocusSits(string target)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        // A track header is selected, so the inspector shows its checkboxes.
        editor.ClickAt(Header(editor, 0));
        Control focusTarget = target == "TrackFlipBox"
            ? editor.Control<SequenceInspectorControl>("SequenceInspector").FindControl<Avalonia.Controls.Primitives.ToggleButton>(target)!
            : editor.Control<Control>(target);
        editor.Click(focusTarget);
        editor.ClickAt(Header(editor, 0));
        editor.Click(focusTarget);

        await PressAsync(editor, Key.C, RawInputModifiers.Control);
        (await ClipboardAsync(editor)).ShouldNotBeNull().ShouldContain("tracks");
        await PressAsync(editor, Key.V, RawInputModifiers.Control);
        SavedTrackNames(path).Count.ShouldBe(4, "paste added a track");
        await PressAsync(editor, Key.D, RawInputModifiers.Control);
        SavedTrackNames(path).Count.ShouldBe(5, "duplicate added a track");
        await PressAsync(editor, Key.Delete);
        SavedTrackNames(path).Count.ShouldBe(4, "delete removed the selected track");
        editor.SequenceNames.Count.ShouldBe(3);
    }

    [AvaloniaTheory]
    [InlineData("SequencePlayButton")]
    [InlineData("SequencePreview")]
    public async Task WithTheListLastTouched_CopyDuplicateAndDelete_WorkWhereverTheFocusSits(string target)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 1);
        editor.Click(editor.Control<Control>(target));

        await PressAsync(editor, Key.C, RawInputModifiers.Control);
        (await ClipboardAsync(editor)).ShouldNotBeNull().ShouldContain("sequence");
        await PressAsync(editor, Key.D, RawInputModifiers.Control);
        editor.SequenceNames.Count.ShouldBe(4);
        // The duplicate opened its name for editing; Escape keeps it.
        await PressAsync(editor, Key.Escape);
        await PressAsync(editor, Key.Delete);
        editor.SequenceNames.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task CtrlC_WithNothingSelectedOnTheTimeline_SaysWhy_AndLeavesNoStaleCopyToPaste()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 0);
        await PressAsync(editor, Key.C, RawInputModifiers.Control);
        (await ClipboardAsync(editor)).ShouldNotBeNull();
        editor.ClickAt(At(editor, 2, 5.0));
        Timeline(editor).SelectedChains.Count.ShouldBe(0);
        Timeline(editor).SelectedTracks.Count.ShouldBe(0);

        await PressAsync(editor, Key.C, RawInputModifiers.Control);

        editor.InfoText.ShouldNotBeNull().ShouldContain("Nothing to copy");
        (await ClipboardAsync(editor)).ShouldBeNullOrEmpty();
        await PressAsync(editor, Key.V, RawInputModifiers.Control);
        editor.SequenceNames.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task CtrlC_BeforeAnythingWasClicked_SaysWhy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        await PressAsync(editor, Key.C, RawInputModifiers.Control);

        editor.InfoText.ShouldNotBeNull().ShouldContain("Nothing to copy");
    }

    [AvaloniaFact]
    public async Task CtrlD_WithNothingSelectedOnTheTimeline_SaysWhy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(At(editor, 2, 5.0));

        await PressAsync(editor, Key.D, RawInputModifiers.Control);

        editor.InfoText.ShouldNotBeNull().ShouldContain("Nothing to duplicate");
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlD_BeforeAnythingWasClicked_SaysWhy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        await PressAsync(editor, Key.D, RawInputModifiers.Control);

        editor.InfoText.ShouldNotBeNull().ShouldContain("Nothing to duplicate");
        editor.UndoLabels.ShouldBeEmpty();
    }

    private static bool[] EditMenuEnabled(AnimationEditorHarness editor) =>
        new[] { "MenuCopy", "MenuCut", "MenuPaste", "MenuDuplicate" }.Select(name => editor.Control<MenuItem>(name).IsEnabled).ToArray();

    [AvaloniaFact]
    public async Task TheEditMenu_FollowsTheLastTouchedPane_NotTheLiveFocus()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        EditMenuEnabled(editor).ShouldAllBe(enabled => !enabled, "nothing was touched yet");

        ClickRow(editor, 0);
        EditMenuEnabled(editor).ShouldAllBe(enabled => enabled);

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        EditMenuEnabled(editor).ShouldAllBe(enabled => enabled, "the Play button took the focus; the list is still the last pane touched");
    }

    // ── F2, Enter, Alt+Up/Down, arrows ──────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task F2_OnTheList_RenamesTheSelectedSequenceInPlace()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        ClickRow(editor, 1);

        await PressAsync(editor, Key.F2);

        (await editor.WaitUntilAsync(() => BoxOf(editor, 1) is { IsEditing: true }, TimeSpan.FromSeconds(1))).ShouldBeTrue();
        editor.Type("Rest");
        await PressAsync(editor, Key.Enter);
        SavedSequences(path).ShouldBe(new[] { "Attack", "Rest", "Walk" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Rest'" });
        FocusIsInList(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
    }

    [AvaloniaFact]
    public async Task F2_WithSeveralSequencesSelected_DoesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        await PressAsync(editor, Key.F2);

        BoxOf(editor, 0)!.IsEditing.ShouldBeFalse();
        BoxOf(editor, 1)!.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task F2_OnTheTimeline_RenamesTheSelectedTrackInPlace()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(Header(editor, 1));

        await PressAsync(editor, Key.F2);

        Timeline(editor).RenamingTrack.ShouldBe(1);
        editor.Type("Skull");
        await PressAsync(editor, Key.Enter);
        SavedTrackNames(path).ShouldBe(new[] { "Body", "Skull", "Tail" });
        FocusIsInTimeline(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
    }

    [AvaloniaFact]
    public async Task F2_OnTheTimeline_WithAChainOrSeveralTracksSelected_DoesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        editor.ClickAt(At(editor, 0, 0.25));
        await PressAsync(editor, Key.F2);
        Timeline(editor).IsRenamingTrack.ShouldBeFalse();

        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);
        await PressAsync(editor, Key.F2);
        Timeline(editor).IsRenamingTrack.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Enter_OnTheList_MovesTheFocusToTheTimeline()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 0);

        await PressAsync(editor, Key.Enter);

        FocusIsInTimeline(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
    }

    [AvaloniaFact]
    public async Task Down_OnTheList_MovesTheSelectionToTheNextSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 0);

        await PressAsync(editor, Key.Down);

        List(editor).SelectedIndex.ShouldBe(1);
        List(editor).SelectedItems!.Count.ShouldBe(1);
        await PressAsync(editor, Key.Up);
        List(editor).SelectedIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task AltDownAndAltUp_OnTheList_MoveTheSelectedSequence_AsOneUndoStepEach()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        ClickRow(editor, 1);

        await PressAsync(editor, Key.Up, RawInputModifiers.Alt);

        SavedSequences(path).ShouldBe(new[] { "Idle", "Attack", "Walk" });
        editor.UndoLabels.ShouldBe(new[] { "Move Sequence 'Idle'" });
        List(editor).SelectedIndex.ShouldBe(0);
        await PressAsync(editor, Key.Up, RawInputModifiers.Alt);
        editor.UndoLabels.Count.ShouldBe(1, "already on top");
        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);
        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);
        SavedSequences(path).ShouldBe(new[] { "Attack", "Walk", "Idle" });
        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);
        editor.UndoLabels.Count.ShouldBe(3, "already at the bottom");
        FocusIsInList(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
        await PressAsync(editor, Key.Z, RawInputModifiers.Control);
        SavedSequences(path).ShouldBe(new[] { "Attack", "Idle", "Walk" });
    }

    [AvaloniaFact]
    public async Task AltDown_WithSeveralSequencesSelected_MovesThemAsOneBlock()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);

        SavedSequences(path).ShouldBe(new[] { "Walk", "Attack", "Idle" });
        editor.UndoLabels.ShouldBe(new[] { "Move 2 Sequences" });
    }

    [AvaloniaFact]
    public async Task AltDownAndAltUp_OnTheTimeline_MoveTheSelectedTrack_AsOneUndoStepEach()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(Header(editor, 0));

        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);

        SavedTrackNames(path).ShouldBe(new[] { "Head", "Body", "Tail" });
        editor.UndoLabels.ShouldBe(new[] { "Move Track 'Body'" });
        Timeline(editor).SelectedTracks.ShouldBe(new[] { 1 });
        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);
        SavedTrackNames(path).ShouldBe(new[] { "Head", "Tail", "Body" });
        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);
        editor.UndoLabels.Count.ShouldBe(2, "already at the bottom");
        await PressAsync(editor, Key.Up, RawInputModifiers.Alt);
        SavedTrackNames(path).ShouldBe(new[] { "Head", "Body", "Tail" });
        FocusIsInTimeline(editor).ShouldBeTrue($"focus is on {Describe(Focused(editor))}");
    }

    [AvaloniaFact]
    public async Task AltDown_OnTheTimeline_WithAChainSelected_ChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteThreeSequences(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(At(editor, 0, 0.25));

        await PressAsync(editor, Key.Down, RawInputModifiers.Alt);

        SavedTrackNames(path).ShouldBe(new[] { "Body", "Head", "Tail" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlAAndEscape_WithThePlayButtonFocused_ActOnTheTimeline_WhenItWasLastTouched()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        editor.ClickAt(At(editor, 0, 0.25));
        editor.Click(editor.Control<Button>("SequencePlayButton"));

        // The first Ctrl+A takes the chain's track, the second the whole sequence.
        await PressAsync(editor, Key.A, RawInputModifiers.Control);
        Timeline(editor).SelectedChains.Count.ShouldBe(3);
        await PressAsync(editor, Key.A, RawInputModifiers.Control);
        Timeline(editor).SelectedChains.Count.ShouldBe(5);

        await PressAsync(editor, Key.Escape);
        Timeline(editor).SelectedChains.Count.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task CtrlA_WithThePlayButtonFocused_SelectsEverySequence_WhenTheListWasLastTouched()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));
        ClickRow(editor, 0);
        editor.Click(editor.Control<Button>("SequencePlayButton"));

        await PressAsync(editor, Key.A, RawInputModifiers.Control);

        List(editor).SelectedItems!.Count.ShouldBe(3);
    }

    // ── Shortcuts panel ─────────────────────────────────────────────────────────────────

    private static Dictionary<string, string> Shortcuts(AnimationEditorHarness editor) =>
        editor.Control<ItemsControl>("ShortcutsList").ItemsSource!.OfType<HotkeyCategoryVm>()
            .SelectMany(category => category.Hotkeys).ToDictionary(entry => entry.Description, entry => entry.Gesture);

    [AvaloniaFact]
    public async Task TheShortcutsPanel_OnASequenceTab_ListsWhatWorksThere()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteThreeSequences(editor));

        var shortcuts = Shortcuts(editor);

        shortcuts.ShouldContainKey("Rename");
        shortcuts["Rename"].ShouldBe("F2");
        shortcuts.Keys.ShouldNotContain("Save As…", "Save As is disabled on a sequence tab");
        shortcuts.Count(pair => pair.Value.StartsWith("Alt+") && (pair.Value.EndsWith("Up") || pair.Value.EndsWith("Down"))).ShouldBe(2);
        var working = editor.Window.Hotkeys.Where(h => h.Scope is HotkeyScope.Document or HotkeyScope.App).Select(h => h.Description).ToHashSet();
        shortcuts.Keys.ToHashSet().ShouldBe(working, ignoreOrder: true, "the panel lists exactly the hotkeys a sequence tab accepts");
    }

    [AvaloniaFact]
    public async Task TheShortcutsPanel_ListsEverythingAgainOnAnAchxTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 16, 16);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        await editor.OpenAsync(WriteThreeSequences(editor));
        Shortcuts(editor).Keys.ShouldNotContain("Save As…");

        editor.ClickTab("hero.achx");

        Shortcuts(editor).Keys.ShouldContain("Save As…");
        Shortcuts(editor).Count.ShouldBe(editor.Window.Hotkeys.Select(h => h.Description).Distinct().Count());
    }
}
