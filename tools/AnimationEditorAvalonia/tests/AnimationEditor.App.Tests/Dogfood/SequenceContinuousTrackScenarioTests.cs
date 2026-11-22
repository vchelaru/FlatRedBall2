using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The track Inspector's Continuous checkbox (one undo step, tri-state for several tracks) and the sequence preview
/// carrying a continuous track's phase across the loop wrap, so the preview matches a player that honours the flag.
/// </summary>
public class SequenceContinuousTrackScenarioTests
{
    // hero.achx: Walk, five 0.1 s frames (0.5 s); Run, two (0.2 s). Body plays Walk x2.4 (1.2 s) and Arm plays Run x6 (1.2 s).
    private static string WriteFixture(AnimationEditorHarness editor, bool bodyContinuous)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack", Loop = true };
        sequence.Tracks.Add(TrackOf("Body", "Walk", 1.2, bodyContinuous));
        sequence.Tracks.Add(TrackOf("Arm", "Run", 1.2, false));
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTrackSave TrackOf(string name, string chain, double length, bool continuous)
    {
        var track = new SequenceTrackSave { Name = name, Continuous = continuous };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = 0, Length = length });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) => editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static PreviewControl Preview(AnimationEditorHarness editor) => editor.Control<PreviewControl>("SequencePreview");

    private static Point Header(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(lane) + 10);
    }

    private static bool SavedFlag(string path, int track) => SequenceListSave.FromFile(path).Sequences[0].Tracks[track].Continuous;

    private sealed class FakeTime
    {
        public long Now;
        public TickClock Clock => new(() => Now, 1000);
    }

    private static List<string> Shown(AnimationEditorHarness editor) =>
        Preview(editor).Composite!.Select(sprite => $"{sprite.Chain.Name}:{sprite.FrameIndex}").ToList();

    /// <summary>Presses Play and then advances in 0.05 s steps.</summary>
    private sealed class Player
    {
        private readonly AnimationEditorHarness _editor;
        private readonly FakeTime _time = new();

        public Player(AnimationEditorHarness editor)
        {
            _editor = editor;
            editor.Window.SetSequencePlaybackClock(_time.Clock);
            editor.Click(editor.Control<Button>("SequencePlayButton"));
        }

        public void Tick(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                _time.Now += 50;
                _editor.Window.TickSequencePlayback();
            }
        }
    }

    [AvaloniaFact]
    public async Task TheCheckbox_IsOffByDefault_ClickingItSetsTheFlagInOneUndoStep_AndCtrlZRestoresIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor, bodyContinuous: false);
        await editor.OpenAsync(path);
        editor.Layout();
        var inspector = Inspector(editor);
        editor.ClickAt(Header(editor, 0));

        inspector.TrackContinuousBox.IsChecked.ShouldBe(false);
        (ToolTip.GetTip(inspector.TrackContinuousBox)?.ToString() ?? "").ShouldContain("wrap");

        editor.Click(inspector.TrackContinuousBox);

        SavedFlag(path, 0).ShouldBeTrue();
        SavedFlag(path, 1).ShouldBeFalse();
        editor.UndoLabels.ShouldBe(new[] { "Turn Continuous On for 'Body'" });

        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedFlag(path, 0).ShouldBeFalse();
        inspector.TrackContinuousBox.IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public async Task TwoTracks_ShowTheCheckboxMixed_AndAClickSetsBoth()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor, bodyContinuous: true);
        await editor.OpenAsync(path);
        editor.Layout();
        var inspector = Inspector(editor);
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);

        inspector.TrackContinuousBox.IsChecked.ShouldBeNull();

        editor.Click(inspector.TrackContinuousBox);

        editor.UndoLabels.Count.ShouldBe(1);
        (SavedFlag(path, 0) == SavedFlag(path, 1)).ShouldBeTrue("one click sets the whole selection to the same value");
    }

    [AvaloniaFact]
    public async Task Preview_AContinuousTrack_KeepsItsPhaseAcrossTheWrap_AnOrdinaryOneRestarts()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, bodyContinuous: true));
        editor.Layout();

        // 1.35 s of playback: the 1.2 s sequence wrapped once. Body (continuous) is at Walk's 1.35 s mark, 0.35 into a
        // 0.5 s cycle, the fourth frame; Arm restarted and is 0.15 s into Run, its second frame.
        var player = new Player(editor);
        player.Tick(27);
        Shown(editor).ShouldBe(new[] { "Run:1", "Walk:3" });

        // 2.45 s: the second wrap. Body is at 2.45 mod 0.5 = 0.45, Walk's last frame; Arm is 0.05 s in, Run's first.
        player.Tick(22);
        Shown(editor).ShouldBe(new[] { "Run:0", "Walk:4" });
    }

    [AvaloniaFact]
    public async Task Preview_WithoutTheFlag_BothTracksRestartAtTheWrap()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, bodyContinuous: false));
        editor.Layout();

        new Player(editor).Tick(27);

        // 0.15 s into the second pass: Walk's second frame, Run's second frame.
        Shown(editor).ShouldBe(new[] { "Run:1", "Walk:1" });
    }
}
