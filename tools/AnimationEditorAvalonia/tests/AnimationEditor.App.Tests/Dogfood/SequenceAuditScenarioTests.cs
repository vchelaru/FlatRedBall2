using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Sequence-tab behaviors the first round of scenarios left unpinned, found by breaking the editor's code and seeing
/// which tests still passed: selection and playback across sequence switches, a corrupt file, a refused source removal,
/// reopening a closed tab, and the playhead after an edit shortens the sequence.
/// </summary>
public class SequenceAuditScenarioTests
{
    // hero.achx: Walk, five 0.1 s frames (0.5 s); Run, two (0.2 s). "Attack" plays Walk from 0.5 s (1 s long);
    // "Idle" plays Run on its own track.
    private static string WriteFixture(AnimationEditorHarness editor, bool loop = false)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var attack = new SequenceSave { Name = "Attack", Loop = loop };
        attack.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0.5, Loops = 1 } },
        });
        var idle = new SequenceSave { Name = "Idle" };
        idle.Tracks.Add(new SequenceTrackSave
        {
            Name = "Legs",
            Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Run", Start = 0, Loops = 1 } },
        });
        save.Sequences.Add(attack);
        save.Sequences.Add(idle);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    // Keyboard focus on the selected row, as after tabbing into the list; a click would take the press handler's path instead.
    private static void FocusList(AnimationEditorHarness editor) =>
        ((Control)List(editor).ContainerFromIndex(List(editor).SelectedIndex)!).Focus();

    private static bool IsPlaying(AnimationEditorHarness editor) =>
        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!.Contains("IconPause");

    private static Point OnChain(AnimationEditorHarness editor, int lane, double time)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), y);
    }

    private static Point RulerAt(AnimationEditorHarness editor, double time)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), TimelineViewport.RulerHeight / 2);
    }

    [AvaloniaFact]
    public async Task ArrowingToAnotherSequence_ClearsTheTimelineSelection_AndShowsThatSequence()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        editor.ClickAt(OnChain(editor, 0, 0.75));
        Timeline(editor).Selection.ShouldNotBeNull();
        Inspector(editor).ChainPanel.IsVisible.ShouldBeTrue();

        FocusList(editor);
        editor.Press(Key.Down);

        List(editor).SelectedIndex.ShouldBe(1);
        Timeline(editor).Selection.ShouldBeNull();
        Timeline(editor).SelectedTrack.ShouldBeNull();
        Inspector(editor).SequencePanel.IsVisible.ShouldBeTrue();
        Inspector(editor).SequenceNameBox.Text.ShouldBe("Idle");
    }

    [AvaloniaFact]
    public async Task UndoingTheDropOfASelectedChain_DropsItsSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var chip = editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Run")!;
        editor.Drag(editor.CenterOf(chip), editor.PointIn(Timeline(editor), Timeline(editor).Viewport.XAt(3.0),
            Timeline(editor).Viewport.LaneTop(0) + 18));
        Timeline(editor).Selection.ShouldBe(new TimelineChainId(0, 1));

        editor.Press(Key.Z, RawInputModifiers.Control);

        Timeline(editor).Selection.ShouldBeNull();
        Inspector(editor).SequencePanel.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task SwitchingToAnotherSequence_StopsPlayback_AtZero()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, loop: true));
        editor.Click(editor.Control<Button>("SequencePlayButton"));
        (await editor.WaitUntilAsync(() => Timeline(editor).Playhead > 0.05, TimeSpan.FromSeconds(3))).ShouldBeTrue();
        IsPlaying(editor).ShouldBeTrue();

        FocusList(editor);
        editor.Press(Key.Down);

        IsPlaying(editor).ShouldBeFalse();
        Timeline(editor).Playhead.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task LeavingTheSequenceTab_WhilePlaying_StopsTheClock()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: true);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        await editor.OpenAsync(boss);
        editor.Click(editor.Control<Button>("SequencePlayButton"));
        (await editor.WaitUntilAsync(() => Timeline(editor).Playhead > 0.05, TimeSpan.FromSeconds(3))).ShouldBeTrue();

        editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        // Playback must not carry on behind the hidden tab.
        Timeline(editor).Playhead.ShouldBe(0);
        IsPlaying(editor).ShouldBeFalse();
        // The first tick on the detached, zero-length sequence would stop the clock too, so the two checks above pass
        // without Hide's own StopPlayback. Its readout tells them apart: Hide stops while the 1 s sequence is still
        // attached, while a tick after detaching would write "0.00 s".
        editor.Control<TextBlock>("SequenceTimeText").Text.ShouldBe("0.00 s / 1.00 s");
    }

    [AvaloniaFact]
    public async Task AnEditThatShortensTheSequence_PullsThePlayheadBackToItsEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        editor.ClickAt(RulerAt(editor, 1.0));
        Timeline(editor).Playhead.ShouldBe(1.0, 1e-6);

        // Walk moves from 0.5 s to 0 s, so the sequence now ends at 0.5 s.
        editor.Drag(OnChain(editor, 0, 0.75), OnChain(editor, 0, 0.25));

        Timeline(editor).SequenceLength.ShouldBe(0.5, 1e-6);
        Timeline(editor).Playhead.ShouldBe(0.5, 1e-6);
    }

    [AvaloniaFact]
    public async Task OpeningACorruptSeqj_ShowsTheBanner_AndOpensNoTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = Path.Combine(editor.ProjectFolder, "broken.seqj");
        File.WriteAllText(path, "{ this is not json");

        await editor.OpenAsync(path);

        editor.ErrorBannerText.ShouldNotBeNull();
        editor.ErrorBannerText!.ShouldContain("Could not open");
        editor.TabLabels.ShouldNotContain("broken.seqj");
    }

    [AvaloniaFact]
    public async Task RemovingASourceAChainStillUses_IsRefusedWithTheBanner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var library = editor.Control<SequenceLibraryControl>("SequenceLibrary");
        Button remove = library.GetVisualDescendants().OfType<Button>().Single(button => (string?)button.Tag == "hero.achx");

        editor.Click(remove);

        editor.ErrorBannerText.ShouldNotBeNull();
        editor.ErrorBannerText!.ShouldContain("used by 2 chains");
        library.Sources.Select(source => source.Source).ShouldBe(new[] { "hero.achx" });
        SequenceListSave.FromFile(boss).Sources.ShouldBe(new[] { "hero.achx" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ClosingAndReopeningASeqjTab_ReadsTheFileFromDiskAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });
        editor.CloseTab("boss.seqj");

        var save = SequenceListSave.FromFile(boss);
        save.Sequences.Add(new SequenceSave { Name = "Die" });
        save.Save(boss);
        await editor.OpenAsync(boss);

        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle", "Die" });
    }
}
