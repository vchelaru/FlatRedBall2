using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The sequence Inspector with several tracks or several chains selected edits them together, as the .achx Inspector
/// does for several frames: a field the items disagree on is blank with a "(mixed)" placeholder, an edit sets it on all of
/// them (or, written as "+ 4", shifts each by its own value), and the whole set is one undo entry.
/// </summary>
public class SequenceInspectorBulkEditScenarioTests
{
    // Body X 1, hidden=false; Head X 3, flipped. Both Y 2. Chains from the shared fixture (every block is 1 cycle):
    // Body: Walk@0, Walk@1, Run@2. Head: Run@0.5, Walk@1.5.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        string path = SequenceTimelineMultiSelectScenarioTests.WriteFixture(editor);
        var save = SequenceListSave.FromFile(path);
        var tracks = save.Sequences[0].Tracks;
        (tracks[0].X, tracks[0].Y) = (1, 2);
        (tracks[1].X, tracks[1].Y) = (3, 2);
        tracks[0].Visible = true;
        tracks[1].Visible = false;
        tracks[0].FlipHorizontal = true;
        tracks[1].FlipHorizontal = false;
        save.Save(path);
        return path;
    }

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static TextBox BoxOf(Control field) => field.GetVisualDescendants().OfType<TextBox>().First();

    private static Point Header(AnimationEditorHarness editor, int lane)
    {
        var timeline = SequenceTimelineMultiSelectScenarioTests.Timeline(editor);
        return editor.PointIn(timeline, 70, timeline.HeaderCenters(lane).Name);
    }

    private static async Task<string> SelectBothTracksAsync(AnimationEditorHarness editor)
    {
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);
        return path;
    }

    private static SequenceSave Saved(string path) => SequenceListSave.FromFile(path).Sequences[0];

    [AvaloniaFact]
    public async Task TwoTracks_DifferentX_ShowMixed_TypingFiveSetsBoth_AndOneUndoRestoresBoth()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await SelectBothTracksAsync(editor);
        var inspector = Inspector(editor);

        inspector.TrackPanel.IsVisible.ShouldBeTrue();
        inspector.TrackXField.Value.ShouldBeNull();
        inspector.TrackXField.PlaceholderText.ShouldBe("(mixed)");
        inspector.TrackYField.Value.ShouldBe(2m, "the tracks agree on Y");
        inspector.TrackYField.PlaceholderText.ShouldNotBe("(mixed)");
        inspector.TrackNameBox.IsEnabled.ShouldBeFalse("a name is for one track");

        editor.TypeAndEnter(BoxOf(inspector.TrackXField), "5");

        Saved(path).Tracks.Select(t => (t.X, t.Y)).Take(2).ShouldBe(new[] { (5f, 2f), (5f, 2f) });
        editor.UndoLabels.ShouldBe(new[] { "Set Offset X of 2 tracks to 5" });
        inspector.TrackXField.Value.ShouldBe(5m);
        SequenceTimelineMultiSelectScenarioTests.Timeline(editor).SelectedTracks.ShouldBe(new[] { 0, 1 });

        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved(path).Tracks.Select(t => t.X).Take(2).ShouldBe(new[] { 1f, 3f });
        inspector.TrackXField.Value.ShouldBeNull();
        inspector.TrackXField.PlaceholderText.ShouldBe("(mixed)");
        editor.UndoLabels.ShouldBeEmpty("one undo undid the edit of both tracks");
    }

    [AvaloniaFact]
    public async Task TwoTracks_RelativeEditOverMixedX_ShiftsEachByItsOwnValue()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await SelectBothTracksAsync(editor);

        editor.TypeAndEnter(BoxOf(Inspector(editor).TrackXField), "+ 4");

        Saved(path).Tracks.Select(t => t.X).Take(2).ShouldBe(new[] { 5f, 7f });
        Inspector(editor).TrackXField.Value.ShouldBeNull("the offsets are still different");
        editor.UndoLabels.ShouldBe(new[] { "Set Offset X of 2 tracks" });
    }

    [AvaloniaFact]
    public async Task TwoTracks_LeavingAMixedFieldUntouched_ChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await SelectBothTracksAsync(editor);
        var inspector = Inspector(editor);

        BoxOf(inspector.TrackXField).Focus();
        editor.Layout();
        editor.Press(Key.Tab);

        Saved(path).Tracks.Select(t => t.X).Take(2).ShouldBe(new[] { 1f, 3f });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TwoTracks_TypingY_KeepsTheMixedXAndEachTracksOwnX()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await SelectBothTracksAsync(editor);

        editor.TypeAndEnter(BoxOf(Inspector(editor).TrackYField), "9");

        Saved(path).Tracks.Select(t => (t.X, t.Y)).Take(2).ShouldBe(new[] { (1f, 9f), (3f, 9f) });
        Inspector(editor).TrackXField.PlaceholderText.ShouldBe("(mixed)");
    }

    [AvaloniaFact]
    public async Task TwoTracks_VisibleAndFlipAreTriState_AndAClickSetsBoth()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = await SelectBothTracksAsync(editor);
        var inspector = Inspector(editor);

        inspector.TrackVisibleBox.IsChecked.ShouldBeNull("Body is shown, Head is hidden");
        inspector.TrackFlipBox.IsChecked.ShouldBeNull("Body is flipped, Head is not");

        // As in the .achx Inspector (the same ToggleButton), a click on the indeterminate state unchecks.
        editor.Click(inspector.TrackVisibleBox);
        Saved(path).Tracks.Select(t => t.Visible).Take(2).ShouldBe(new[] { false, false });
        inspector.TrackVisibleBox.IsChecked.ShouldBe(false);
        editor.UndoLabels.ShouldBe(new[] { "Hide 2 tracks" });

        editor.Click(inspector.TrackFlipBox);
        Saved(path).Tracks.Select(t => t.FlipHorizontal).Take(2).ShouldBe(new[] { false, false });
        editor.UndoLabels.ShouldBe(new[] { "Hide 2 tracks", "Turn Flip Off for 2 tracks" });
        editor.Click(inspector.TrackFlipBox);
        Saved(path).Tracks.Select(t => t.FlipHorizontal).Take(2).ShouldBe(new[] { true, true });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved(path).Tracks.Select(t => t.FlipHorizontal).Take(2).ShouldBe(new[] { true, false });
        inspector.TrackFlipBox.IsChecked.ShouldBeNull();
        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved(path).Tracks.Select(t => t.Visible).Take(2).ShouldBe(new[] { true, false });
        inspector.TrackVisibleBox.IsChecked.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task TwoChains_StartAndCyclesShowMixed_AndEditingSetsBoth_SourceAndChainRowsShowTheCommonValue()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        // Body Walk@0 and Head Walk@1.5: same chain, different start, same cycles.
        editor.ClickAt(SequenceTimelineMultiSelectScenarioTests.At(editor, 0, 0.25));
        editor.ClickAt(SequenceTimelineMultiSelectScenarioTests.At(editor, 1, 1.75), RawInputModifiers.Control);
        var inspector = Inspector(editor);

        inspector.ChainPanel.IsVisible.ShouldBeTrue();
        inspector.ChainStartField.Value.ShouldBeNull();
        inspector.ChainStartField.PlaceholderText.ShouldBe("(mixed)");
        inspector.ChainLoopsField.Value.ShouldBe(1m);
        inspector.ChainSourceText.Text.ShouldBe("hero.achx");
        inspector.ChainNameText.Text.ShouldBe("Walk");

        editor.TypeAndEnter(BoxOf(inspector.ChainStartField), "3");
        Saved(path).Tracks[0].Chains[0].Start.ShouldBe(3d);
        Saved(path).Tracks[1].Chains[1].Start.ShouldBe(3d);
        editor.TypeAndEnter(BoxOf(inspector.ChainLoopsField), "2");
        Saved(path).Tracks[0].Chains[0].Loops.ShouldBe(2f);
        Saved(path).Tracks[1].Chains[1].Loops.ShouldBe(2f);
        editor.UndoLabels.ShouldBe(new[] { "Set Start of 2 chains to 3", "Set Cycles of 2 chains to 2" });

        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved(path).Tracks[0].Chains[0].Start.ShouldBe(0d);
        Saved(path).Tracks[1].Chains[1].Start.ShouldBe(1.5d);
        inspector.ChainStartField.PlaceholderText.ShouldBe("(mixed)");
    }

    [AvaloniaFact]
    public async Task TwoChains_OfDifferentNames_SayHowManyChains_AndOneChainKeepsItsOwnPanel()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        editor.ClickAt(SequenceTimelineMultiSelectScenarioTests.At(editor, 0, 0.25));
        var inspector = Inspector(editor);
        inspector.GoToDefinitionButton.IsVisible.ShouldBeTrue();
        inspector.ChainNameText.Text.ShouldBe("Walk");

        editor.ClickAt(SequenceTimelineMultiSelectScenarioTests.At(editor, 0, 2.1), RawInputModifiers.Control);

        inspector.ChainNameText.Text.ShouldBe("2 chains");
        inspector.ChainSourceText.Text.ShouldBe("hero.achx");
        inspector.GoToDefinitionButton.IsVisible.ShouldBeFalse("Go to Definition is for one chain");
    }
}
