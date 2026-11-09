using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Resting the pointer on a timeline block shows its full text (<c>Walk ×2 (1.00 s)</c>) after a short delay, at any zoom.</summary>
public class SequenceBlockTooltipScenarioTests
{
    // Walk is five 0.1 s frames (0.5 s a cycle). Body plays it twice from 0, Once plays it once from 0.
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16));
        editor.WriteAchx("hero.achx", walk);
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        foreach (var (name, loops) in new[] { ("Body", 2f), ("Once", 1f) })
        {
            var track = new SequenceTrackSave { Name = name };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = loops });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static Point OnBlock(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(0.2), timeline.Viewport.LaneTop(lane) + TimelineLayout.BodyTop + 4);
    }

    [AvaloniaFact]
    public async Task HoveringABlock_ShowsItsFullText_OnlyAfterTheDelay()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.Hover(OnBlock(editor, 0));
        timeline.BlockTooltip.ShouldBeNull();

        (await editor.WaitUntilAsync(() => timeline.BlockTooltip is not null, HoverDelays.QuickTooltip + TimeSpan.FromSeconds(2))).ShouldBeTrue();
        timeline.BlockTooltip.ShouldBe("Walk ×2 (1.00 s)");
    }

    [AvaloniaFact]
    public async Task ASingleLoopBlock_StillSaysXOneInItsTooltip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.Hover(OnBlock(editor, 1));
        await editor.WaitUntilAsync(() => timeline.BlockTooltip is not null, HoverDelays.QuickTooltip + TimeSpan.FromSeconds(2));

        timeline.BlockTooltip.ShouldBe("Walk ×1 (0.50 s)");
    }

    [AvaloniaFact]
    public async Task MovingOffTheBlock_HidesTheTooltip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);
        editor.Hover(OnBlock(editor, 0));
        await editor.WaitUntilAsync(() => timeline.BlockTooltip is not null, HoverDelays.QuickTooltip + TimeSpan.FromSeconds(2));

        editor.Hover(editor.PointIn(timeline, timeline.Viewport.XAt(5), timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop + 4));

        timeline.BlockTooltip.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task OverTheFilmstrip_TheFrameCardCarriesTheBlockText_AndNoSecondPopupDraws()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.Hover(editor.PointIn(timeline, timeline.Viewport.XAt(0.2), timeline.Viewport.LaneTop(0) + TimelineLayout.FilmTop + 10));

        timeline.HoverCard.ShouldNotBeNull();
        timeline.HoverCardBlockText.ShouldBe("Walk ×2 (1.00 s)");
    }
}
