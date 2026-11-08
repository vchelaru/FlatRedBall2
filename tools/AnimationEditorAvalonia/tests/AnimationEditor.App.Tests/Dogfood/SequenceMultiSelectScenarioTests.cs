using AnimationEditor.App.Controls;
using AnimationEditor.App.Models;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Ctrl+click and Shift+click in the sequence list select several sequences, and the preview plays them together, each
/// on its own clock and loop flag, like several selected chains in an <c>.achx</c> tab. The first selected one stays the
/// primary: the timeline, Inspector and rename act on it alone (delete, duplicate, copy, cut and paste take the whole selection).
/// </summary>
public class SequenceMultiSelectScenarioTests
{
    // Attack (row 0): Walk on hero.png, 0.5 s, no loop. Aura (row 1): Spark on fx.png, 0.2 s, no loop, Z 3.
    // Idle (row 2): Run on hero.png, 0.2 s, loops.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("hero.png", 64, 64);
        editor.WritePng("fx.png", 32, 32);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "hero.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "hero.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        editor.WriteAchx("fx.achx", AnimationEditorHarness.Chain("Spark", "fx.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx", "fx.achx" } };
        save.Sequences.Add(Sequence("Attack", loop: false, Track("Body", 0, "hero.achx", "Walk")));
        save.Sequences.Add(Sequence("Aura", loop: false, Track("Glow", 3, "fx.achx", "Spark")));
        save.Sequences.Add(Sequence("Idle", loop: true, Track("Body", 0, "hero.achx", "Run")));
        save.Save(path);
        return path;
    }

    private static SequenceSave Sequence(string name, bool loop, SequenceTrackSave track)
    {
        var sequence = new SequenceSave { Name = name, Loop = loop };
        sequence.Tracks.Add(track);
        return sequence;
    }

    private static SequenceTrackSave Track(string name, int z, string source, string chain)
    {
        var track = new SequenceTrackSave { Name = name, Z = z };
        track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = 0, Loops = 1 });
        return track;
    }

    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static Avalonia.Controls.Control Row(AnimationEditorHarness editor, int row) => (Avalonia.Controls.Control)List(editor).ContainerFromIndex(row)!;

    private static void ClickRow(AnimationEditorHarness editor, int row, RawInputModifiers modifiers = RawInputModifiers.None) =>
        editor.Click(Row(editor, row), modifiers);

    private static List<string> Selected(AnimationEditorHarness editor) =>
        List(editor).SelectedItems!.OfType<SequenceListItem>().Select(item => item.Name).ToList();

    private static List<string> Shown(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview").Composite!.Select(sprite => $"{sprite.Chain.Name}:{sprite.FrameIndex}").ToList();

    private static string InspectorName(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector").SequenceNameBox.Text!;

    private static bool IsPlaying(AnimationEditorHarness editor) =>
        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!.Contains("IconPause");

    private static Point RulerAt(AnimationEditorHarness editor, double time) =>
        editor.PointIn(Timeline(editor), Timeline(editor).Viewport.XAt(time), TimelineViewport.RulerHeight / 2);

    private static void Undo(AnimationEditorHarness editor) => editor.Press(Key.Z, RawInputModifiers.Control);

    [AvaloniaFact]
    public async Task CtrlClick_AddsTheSequenceToThePreview_TheFirstClickedStaysAtTheBackAndPrimary()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        ClickRow(editor, 0);
        Shown(editor).ShouldBe(new[] { "Walk:0" });
        ClickRow(editor, 1, RawInputModifiers.Control);

        Selected(editor).ShouldBe(new[] { "Attack", "Aura" });
        Shown(editor).ShouldBe(new[] { "Walk:0", "Spark:0" });
        editor.Control<PreviewControl>("SequencePreview").Composite!.Select(sprite => Path.GetFileName(sprite.TexturePath))
            .ShouldBe(new[] { "hero.png", "fx.png" });
        // The timeline, Inspector and time readout stay on the primary.
        InspectorName(editor).ShouldBe("Attack");
        editor.Control<TextBlock>("SequenceTimeText").Text.ShouldBe("0.00 s / 0.50 s");
    }

    [AvaloniaFact]
    public async Task Sequences_DrawInSelectionOrder_NotByTrackZOrListPosition()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        // Aura's track has Z 3 and Idle sits below it in the list, but the first one selected draws at the back.
        ClickRow(editor, 2);
        ClickRow(editor, 1, RawInputModifiers.Control);
        ClickRow(editor, 0, RawInputModifiers.Control);

        Selected(editor).ShouldBe(new[] { "Idle", "Aura", "Attack" });
        Shown(editor).ShouldBe(new[] { "Run:0", "Spark:0", "Walk:0" });
        InspectorName(editor).ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task ShiftClick_SelectsTheRange()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        ClickRow(editor, 0);
        ClickRow(editor, 2, RawInputModifiers.Shift);

        Selected(editor).ShouldBe(new[] { "Attack", "Aura", "Idle" });
        Shown(editor).ShouldBe(new[] { "Walk:0", "Spark:0", "Run:0" });
        InspectorName(editor).ShouldBe("Attack");
    }

    [AvaloniaFact]
    public async Task APlainClick_ReturnsToASingleSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        ClickRow(editor, 2);

        Selected(editor).ShouldBe(new[] { "Idle" });
        Shown(editor).ShouldBe(new[] { "Run:0" });
        InspectorName(editor).ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task CtrlClick_OnTheOnlySelectedRow_KeepsItSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        ClickRow(editor, 1);

        ClickRow(editor, 1, RawInputModifiers.Control);

        Selected(editor).ShouldBe(new[] { "Aura" });
        Shown(editor).ShouldBe(new[] { "Spark:0" });
    }

    [AvaloniaFact]
    public async Task CtrlClick_OnThePrimary_HandsThePrimaryToTheNextSelectedOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        ClickRow(editor, 0, RawInputModifiers.Control);

        Selected(editor).ShouldBe(new[] { "Aura" });
        InspectorName(editor).ShouldBe("Aura");
        Shown(editor).ShouldBe(new[] { "Spark:0" });
    }

    [AvaloniaFact]
    public async Task Scrubbing_MovesThePrimaryOnly_TheOthersAreNotRealigned()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        editor.ClickAt(RulerAt(editor, 0.15));

        Timeline(editor).Playhead.ShouldBe(0.15, 0.01);
        Shown(editor).ShouldBe(new[] { "Walk:1", "Spark:0" });
    }

    [AvaloniaFact]
    public async Task Play_RunsEverySelectedSequenceOnItsOwnClock_EachLoopingByItsOwnFlag()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        // Idle (0.2 s, loops) is primary; Attack (0.5 s) plays once.
        ClickRow(editor, 2);
        ClickRow(editor, 0, RawInputModifiers.Control);

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        IsPlaying(editor).ShouldBeTrue();

        // Idle wraps while Attack is still running: its clock is not stretched to the longest length.
        (await editor.WaitUntilAsync(() => Timeline(editor).Playhead > 0.15, TimeSpan.FromSeconds(3))).ShouldBeTrue();
        (await editor.WaitUntilAsync(() => Timeline(editor).Playhead < 0.1, TimeSpan.FromSeconds(3))).ShouldBeTrue();
        Shown(editor).ShouldContain(shown => shown.StartsWith("Walk:"));

        // Attack ends at 0.5 s and disappears; Idle keeps looping, so the preview is still playing.
        (await editor.WaitUntilAsync(() => !Shown(editor).Any(shown => shown.StartsWith("Walk:")), TimeSpan.FromSeconds(3))).ShouldBeTrue();
        IsPlaying(editor).ShouldBeTrue();
        Shown(editor).ShouldContain(shown => shown.StartsWith("Run:"));
    }

    [AvaloniaFact]
    public async Task Pause_FreezesEverySequence_AndStopRewindsThemAll()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 2, RawInputModifiers.Control);
        editor.Click(editor.Control<Button>("SequencePlayButton"));
        (await editor.WaitUntilAsync(() => Shown(editor).Contains("Walk:1"), TimeSpan.FromSeconds(3))).ShouldBeTrue();

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        var frozen = Shown(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));
        Shown(editor).ShouldBe(frozen);
        IsPlaying(editor).ShouldBeFalse();

        editor.Click(editor.Control<Button>("SequenceStopButton"));
        Shown(editor).ShouldBe(new[] { "Walk:0", "Run:0" });
    }

    [AvaloniaFact]
    public async Task EditingCommands_ActOnThePrimaryOnly()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        editor.Click(editor.Control<ToggleButton>("SequenceLoopToggle"));

        var saved = SequenceListSave.FromFile(boss).Sequences;
        saved[0].Loop.ShouldBeTrue();
        saved[1].Loop.ShouldBeFalse();
        // The edit does not disturb the multi-selection.
        Selected(editor).ShouldBe(new[] { "Attack", "Aura" });

        Undo(editor);
        SequenceListSave.FromFile(boss).Sequences[0].Loop.ShouldBeFalse();
        Selected(editor).ShouldBe(new[] { "Attack", "Aura" });
        Shown(editor).ShouldBe(new[] { "Walk:0", "Spark:0" });
    }

    [AvaloniaFact]
    public async Task Deleting_TheWholeSelection_SelectsTheSurvivor_AndUndoRestoresTheSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        editor.Press(Key.Delete);

        editor.SequenceNames.ShouldBe(new[] { "Idle" });
        Selected(editor).ShouldBe(new[] { "Idle" });
        Shown(editor).ShouldBe(new[] { "Run:0" });
        InspectorName(editor).ShouldBe("Idle");

        Undo(editor);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Aura", "Idle" });
        Selected(editor).ShouldBe(new[] { "Attack", "Aura" });
        Shown(editor).ShouldBe(new[] { "Walk:0", "Spark:0" });
    }

    [AvaloniaFact]
    public async Task AddSequence_SelectsOnlyTheNewOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        editor.Click(editor.Control<Button>("AddSequenceButton"));

        Selected(editor).ShouldBe(new[] { "Sequence" });
        Shown(editor).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task RightClick_OnANonPrimarySelectedRow_KeepsTheSelection_SoDeleteActsOnAllOfIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);

        editor.RightClick(Row(editor, 1));
        Selected(editor).ShouldBe(new[] { "Attack", "Aura" });
        editor.PickMenuItem(List(editor).ContextMenu!, "Delete");

        SequenceListSave.FromFile(boss).Sequences.Select(sequence => sequence.Name).ShouldBe(new[] { "Idle" });
    }

    [AvaloniaFact]
    public async Task TheMultiSelection_IsViewState_NotSavedAndNotUndoable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        byte[] before = File.ReadAllBytes(boss);

        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);
        ClickRow(editor, 2, RawInputModifiers.Control);

        File.ReadAllBytes(boss).ShouldBe(before);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TheMultiSelection_SurvivesSwitchingTabsAway_AndBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        ClickRow(editor, 0);
        ClickRow(editor, 1, RawInputModifiers.Control);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));

        editor.ClickTab("boss.seqj");

        Selected(editor).ShouldBe(new[] { "Attack", "Aura" });
        Shown(editor).ShouldBe(new[] { "Walk:0", "Spark:0" });
    }
}
