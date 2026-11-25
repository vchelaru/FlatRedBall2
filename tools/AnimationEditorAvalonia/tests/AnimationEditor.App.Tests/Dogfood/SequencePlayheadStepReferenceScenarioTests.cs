using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The step keys on a sequence that plays other sequences.</summary>
public class SequencePlayheadStepReferenceScenarioTests
{
    [AvaloniaFact]
    public async Task StepRight_OnASequenceThatPlaysAnother_StopsWhereTheClipStarts()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        var save = SequenceListSave.FromFile(seqj);
        var host = new SequenceSave { Name = "Host" };
        host.Tracks.Add(new SequenceTrackSave { Name = "R", References = [new SequenceReferenceSave { Sequence = "Attack", Start = 1 }] });
        save.Sequences.Insert(0, host);
        save.Save(seqj);
        await editor.OpenAsync(seqj);
        var timeline = Timeline(editor);
        timeline.Focus();
        editor.Layout();

        editor.Press(Key.Right);

        timeline.Playhead.ShouldBe(1.0, 0.001, $"length {timeline.SequenceLength}");
    }
}
