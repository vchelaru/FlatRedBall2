using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A sequence dragged from the list onto an existing reference track's clip band is appended to it as another clip.</summary>
public class SequenceReferenceMultiClipScenarioTests
{
    private static string WriteFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.Add(new SequenceSave
        {
            Name = "Idle",
            Tracks = { new SequenceTrackSave { Name = "Link", References = [new SequenceReferenceSave { Sequence = "Earth" }] } },
        });
        foreach (var name in new[] { "Earth", "Action" })
            save.Sequences.Add(new SequenceSave
            {
                Name = name,
                Tracks = { new SequenceTrackSave { Name = "Body", Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk" } } } },
            });
        save.Save(path);
        return path;
    }

    [AvaloniaFact]
    public async Task DroppingASequenceOnAReferenceTrack_AppendsAClipAtThePointer_AsOneUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        var row = (Control)editor.Control<ListBox>("SequenceList").ContainerFromIndex(2)!;
        double y = timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop + 4;
        var drop = editor.PointIn(timeline, timeline.Viewport.XAt(3.0), y);

        editor.DragHold(editor.CenterOf(row), drop);
        timeline.SequenceDropClipTrack.ShouldBe(0);
        timeline.SequenceDropSlot.ShouldBeNull();
        editor.DragRelease(drop);

        var clips = SequenceListSave.FromFile(boss).Sequences[0].Tracks.ShouldHaveSingleItem().References!;
        clips.Select(c => c.Sequence).ShouldBe(new[] { "Earth", "Action" });
        clips[1].Start.ShouldBe(3.0, 0.2);
        editor.UndoLabels.ShouldBe(new[] { "Add Reference 'Action' to 'Link'" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[0].References!.Count.ShouldBe(1);
    }
}
