using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>#1829: a sequence plays its sources in seconds whatever unit each source file is written in.</summary>
public class FrameLengthUnitSequenceScenarioTests
{
    // hero.achx (Millisecond): Walk, five 100 ms frames (0.5 s). fx.achx (Second): Spark, two 0.1 s frames.
    private static string WriteFixture(AnimationEditorHarness editor, params SequenceTrackSave[] tracks)
    {
        editor.WritePng("hero.png", 64, 64);
        editor.WritePng("fx.png", 32, 32);
        editor.WriteAchx("hero.achx", TimeMeasurementUnit.Millisecond,
            AnimationEditorHarness.Chain("Walk", "hero.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)));
        editor.WriteAchx("fx.achx", TimeMeasurementUnit.Second,
            AnimationEditorHarness.Chain("Spark", "fx.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx", "fx.achx" } };
        var sequence = new SequenceSave { Name = "Attack", Loop = false };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTrackSave Track(string name, string source, string chain)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = 0, Loops = 1 });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static PreviewControl SequencePreview(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview");

    [AvaloniaFact]
    public async Task MillisecondAndSecondSources_ScrubToTheSameFrames_AndTheSequenceIsHalfASecondLong()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Fx", "fx.achx", "Spark"), Track("Body", "hero.achx", "Walk"));
        await editor.OpenAsync(boss);

        var timeline = Timeline(editor);
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.15), AnimationEditor.Core.Sequences.TimelineViewport.RulerHeight / 2));

        SequencePreview(editor).Composite!.Select(sprite => $"{sprite.Chain.Name}:{sprite.FrameIndex}")
            .ShouldBe(new[] { "Walk:1", "Spark:1" });
        editor.Control<TextBlock>("SequenceTimeText").Text.ShouldBe("0.15 s / 0.50 s");
    }
}
