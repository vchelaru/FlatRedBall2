using AnimationEditor.App.Controls;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A sequence with Loop off plays to its end, stops there and keeps its final pose on screen; with Loop on it repeats.
/// </summary>
public class SequenceHoldFinalPoseScenarioTests
{
    // hero.achx: Walk, two 0.1 s frames (0.2 s). "Attack" plays Walk once from 0.
    private static string WriteFixture(AnimationEditorHarness editor, bool loop)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var attack = new SequenceSave { Name = "Attack", Loop = loop };
        attack.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 } },
        });
        save.Sequences.Add(attack);
        save.Save(path);
        return path;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static PreviewControl Preview(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview");

    private static bool IsPlaying(AnimationEditorHarness editor) =>
        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!.Contains("IconPause");

    [AvaloniaFact]
    public async Task ANonLoopingSequence_PlayedToItsEnd_StopsAndKeepsItsFinalPoseOnScreen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, loop: false));

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        (await editor.WaitUntilAsync(() => !IsPlaying(editor), TimeSpan.FromSeconds(3))).ShouldBeTrue();

        Timeline(editor).Playhead.ShouldBe(0.2, 1e-6);
        var shown = Preview(editor).Composite!;
        shown.Count.ShouldBe(1);
        shown[0].FrameIndex.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task ALoopingSequence_KeepsRepeating_PastItsLength()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor, loop: true));

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(600));

        IsPlaying(editor).ShouldBeTrue();
        Timeline(editor).Playhead.ShouldBeLessThan(0.2);
    }
}
