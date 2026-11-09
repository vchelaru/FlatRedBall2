using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Hovering a track name the name column cut short shows its full name after a short delay; a name that fits shows nothing.</summary>
public class SequenceTrackNameTooltipScenarioTests
{
    private const string LongName = "Boss attack with a very long descriptive track name";
    private const string MediumName = "Medium length track name";

    private static string WriteFixture(AnimationEditorHarness editor)
    {
        var chains = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var walk = new AnimationChainSave { Name = "Walk" };
        walk.Frames.Add(new AnimationFrameSave { TextureName = "hero.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
        chains.AnimationChains.Add(walk);
        chains.Save(Path.Combine(editor.ProjectFolder, "hero.achx"));
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var action = new SequenceSave { Name = "Action" };
        foreach (string name in new[] { LongName, "Sun", MediumName })
        {
            var track = new SequenceTrackSave { Name = name };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
            action.Tracks.Add(track);
        }
        save.Sequences.Add(action);
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static Point OnName(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 60, Timeline(editor).HeaderCenters(lane).Name);

    private static Task PastTheDelay(AnimationEditorHarness editor) =>
        editor.WaitAsync(TimelineTooltips.ShowDelay + TimeSpan.FromMilliseconds(150));

    [AvaloniaFact]
    public async Task HoveringACutOffName_ShowsTheFullNameAfterTheDelay_AndLeavingClearsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.Hover(OnName(editor, 0));
        timeline.NameTooltip.ShouldBeNull();
        await PastTheDelay(editor);
        timeline.NameTooltip.ShouldBe(LongName);

        editor.Hover(new Point(500, 5));
        timeline.NameTooltip.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task HoveringANameThatFits_ShowsNoTooltip()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        editor.Hover(OnName(editor, 1));
        await PastTheDelay(editor);

        Timeline(editor).NameTooltip.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task MovingOffTheNameBeforeTheDelay_ShowsNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        editor.Hover(OnName(editor, 0));
        editor.Hover(OnName(editor, 1));
        await PastTheDelay(editor);

        Timeline(editor).NameTooltip.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task WideningTheColumn_StopsTheTooltip_BecauseTheNameNowFits()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        editor.Hover(OnName(editor, 2));
        await PastTheDelay(editor);
        timeline.NameTooltip.ShouldBe(MediumName);

        timeline.Viewport.SetColumnWidth(TimelineViewport.MaxColumnWidth);
        editor.Hover(new Point(500, 5));
        editor.Hover(OnName(editor, 2));
        await PastTheDelay(editor);

        timeline.NameTooltip.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task TheTooltip_AppearsAtThePointersCurrentSpot_SoTheNextMoveDoesNotSnapIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        var timeline = Timeline(editor);

        var enter = OnName(editor, 0);
        editor.Hover(enter);
        var entered = timeline.NameTooltipAnchor;
        var settled = enter + new Point(7, 1);
        editor.Hover(settled);
        await PastTheDelay(editor);

        timeline.NameTooltip.ShouldBe(LongName);
        timeline.NameTooltipAnchor.ShouldBe(entered + new Point(7, 1));
        var before = timeline.NameTooltipRect!.Value;

        editor.Hover(settled + new Point(1, 0));
        timeline.NameTooltipRect!.Value.ShouldBe(before.Translate(new Vector(1, 0)));
    }
}
