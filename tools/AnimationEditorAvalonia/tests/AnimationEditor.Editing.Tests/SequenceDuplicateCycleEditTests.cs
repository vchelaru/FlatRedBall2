using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class SequenceDuplicateCycleEditTests
{
    [Fact]
    public void DuplicateSequence_AsTheNameItsOwnClipPlays_IsRefusedRatherThanReportedAsMade()
    {
        using var w = new SeqWorkspace();
        var s = w.Open();
        s.Document.Save.Sequences[0].Tracks.Add(new SequenceTrackSave { Name = "R", References = [new SequenceReferenceSave { Sequence = "Gone" }] });

        var result = SequenceEdit.DuplicateSequence(s.Document, s.Undo, s.Document.Save.Sequences[0].Name, "Gone");

        (result.Success, s.Document.Save.Sequences.Any(q => q.Name == "Gone")).ShouldBe((false, false));
    }
}
