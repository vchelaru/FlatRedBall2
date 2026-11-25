using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>#2137 in the window: an animation that loses its length while it plays, and the block of one that has none.</summary>
public class ZeroLengthScenarioTests
{
    private static readonly TimeSpan ReloadTimeout = TimeSpan.FromSeconds(6);

    private static string WriteHero(AnimationEditorHarness editor, float length)
    {
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        foreach (var frame in walk.Frames) frame.FrameLength = length;
        return editor.WriteAchx("hero.achx", walk, AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16), (16, 16, 16, 16)));
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) => editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static string WriteSeqj(AnimationEditorHarness editor)
    {
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        foreach (string chain in new[] { "Walk", "Run" })
            sequence.Tracks.Add(new SequenceTrackSave { Name = chain, Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Length = chain == "Walk" ? null : 1 } } });
        save.Sequences.Add(sequence);
        save.Sequences.Add(new SequenceSave { Name = "None", Tracks = { new SequenceTrackSave { Name = "Walk", Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk" } } } } });
        save.Save(path);
        return path;
    }

    [AvaloniaFact]
    public async Task AchxRewrittenWithZeroLengthsWhileItPlays_ShowsAFrameAndCanBePlayedAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(WriteHero(editor, 0.1f));
        editor.ClickRow(editor.Project.AnimationChains[0]);
        var playback = editor.Preview.Playback;
        playback.Play();
        playback.Advance(0.15);

        WriteHero(editor, 0f);
        editor.WaitUntil(() => editor.Project.AnimationChains[0].Frames[0].FrameLength == 0, ReloadTimeout).ShouldBeTrue();
        for (int i = 0; i < 50; i++) playback.Advance(1);
        editor.Wait(TimeSpan.FromMilliseconds(200));
        playback.CurrentFrameIndex.ShouldBe(1);

        WriteHero(editor, 0.1f);
        editor.WaitUntil(() => editor.Project.AnimationChains[0].Frames[0].FrameLength > 0, ReloadTimeout).ShouldBeTrue();
        playback.Play();
        playback.Advance(0.05);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task BlockOfAnAnimationOfNoLength_IsSelectedByAClick_AndItsTooltipSaysZeroSeconds()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        WriteHero(editor, 0f);
        await editor.OpenAsync(WriteSeqj(editor));
        var timeline = Timeline(editor);
        var point = editor.PointIn(timeline, timeline.Viewport.XAt(0.5), timeline.Viewport.LaneTop(0) + TimelineLayout.FilmTop + 5);

        editor.ClickAt(point);

        timeline.Selection.ShouldBe(new TimelineChainId(0, 0));
        BlockLabel.Tooltip("Walk", timeline.Blocks[0].Cycles, timeline.Blocks[0].PlayedSeconds).ShouldBe("Walk ×1 (0 s)");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task SourceRewrittenWithZeroLengthsWhileTheSequencePlays_KeepsPlayingTheRest()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        WriteHero(editor, 0.1f);
        await editor.OpenAsync(WriteSeqj(editor));
        long now = 0;
        editor.Window.SetSequencePlaybackClock(new TickClock(() => now, 1000));
        editor.Click(editor.Control<Button>("SequencePlayButton"));
        void Tick()
        {
            for (int i = 0; i < 30; i++)
            {
                now += 50;
                editor.Window.TickSequencePlayback();
            }
        }
        Tick();

        WriteHero(editor, 0f);
        (await editor.WaitUntilAsync(() => Timeline(editor).Blocks[0].CycleLength == 0, ReloadTimeout)).ShouldBeTrue();
        Tick();

        editor.Control<PreviewControl>("SequencePreview").Composite!.Select(sprite => sprite.Chain.Name).ShouldBe(["Run"]);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task LoopingSequenceOfNoLength_PressingPlayReturns()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        WriteHero(editor, 0f);
        await editor.OpenAsync(WriteSeqj(editor));
        editor.Control<ListBox>("SequenceList").SelectedIndex = 1;
        editor.Wait(TimeSpan.FromMilliseconds(100));
        long now = 0;
        editor.Window.SetSequencePlaybackClock(new TickClock(() => now, 1000));

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        for (int i = 0; i < 30; i++)
        {
            now += 50;
            editor.Window.TickSequencePlayback();
        }

        Timeline(editor).Playhead.ShouldBe(0);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task BlockWithItsOwnLengthOfAnAnimationOfNoLength_HoldsItsFrame_AndItsEdgeResizesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        WriteHero(editor, 0f);
        string path = WriteSeqj(editor);
        var save = SequenceListSave.FromFile(path);
        save.Sequences[0].Tracks[0].Chains[0].Length = 2;
        save.Save(path);
        await editor.OpenAsync(path);
        var timeline = Timeline(editor);
        var sprite = editor.Control<PreviewControl>("SequencePreview").Composite!.Single(s => s.Chain.Name == "Walk");
        sprite.FrameIndex.ShouldBe(1);
        timeline.FilmstripCells(timeline.Blocks[0]).ShouldHaveSingleItem().Frame.ShouldBe(1);
        double y = timeline.Viewport.LaneTop(0) + TimelineLayout.FilmTop + 5;

        editor.Drag(editor.PointIn(timeline, timeline.Viewport.XAt(2) - 2, y), editor.PointIn(timeline, timeline.Viewport.XAt(3) - 2, y));

        SequenceListSave.FromFile(path).Sequences[0].Tracks[0].Chains[0].Length!.Value.ShouldBe(3, 0.05);
        timeline.Blocks[0].PlayedSeconds.ShouldBe(3, 0.05);
        editor.ThrowIfErrorShown();
    }
}
