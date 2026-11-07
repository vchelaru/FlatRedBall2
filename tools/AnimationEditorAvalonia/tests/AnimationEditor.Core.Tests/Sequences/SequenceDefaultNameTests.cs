using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// New sequences and new tracks share one default-name rule: "Sequence"/"Track", then the first free "Sequence 2",
/// "Sequence 3", and so on, where free ignores case. A gap left by a deletion or a rename is filled.
/// </summary>
public class SequenceDefaultNameTests
{
    private static SequenceDocument DocumentWith(params string[] sequenceNames)
    {
        var save = new SequenceListSave();
        foreach (string name in sequenceNames) save.Sequences.Add(new SequenceSave { Name = name });
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static SequenceDocument DocumentWithTracks(params string[] trackNames)
    {
        var sequence = new SequenceSave { Name = "Attack" };
        foreach (string name in trackNames) sequence.Tracks.Add(new SequenceTrackSave { Name = name });
        var save = new SequenceListSave();
        save.Sequences.Add(sequence);
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    [Fact]
    public void AddSequence_OnAFileWithSequence_GivesSequence2_ThenSequence3()
    {
        var document = DocumentWith("Sequence");
        var undo = new UndoManager();

        document.AddSequence(undo).ShouldBe("Sequence 2");
        document.AddSequence(undo).ShouldBe("Sequence 3");
    }

    [Fact]
    public void AddSequence_FillsTheFirstGap()
    {
        var document = DocumentWith("Sequence", "Sequence 3");

        document.AddSequence(new UndoManager()).ShouldBe("Sequence 2");
    }

    [Fact]
    public void AddSequence_AfterDeletingTheFirst_ReusesTheDefaultName()
    {
        var document = DocumentWith("Idle");
        var undo = new UndoManager();
        document.AddSequence(undo).ShouldBe("Sequence");
        document.AddSequence(undo).ShouldBe("Sequence 2");

        document.DeleteSequence(undo, 1);

        document.AddSequence(undo).ShouldBe("Sequence");
        document.Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle", "Sequence 2", "Sequence" });
    }

    [Fact]
    public void AddSequence_IgnoresCase_WhenLookingForAFreeName()
    {
        var document = DocumentWith("sequence", "SEQUENCE 2");
        var undo = new UndoManager();

        document.AddSequence(undo).ShouldBe("Sequence 3");
    }

    [Fact]
    public void AddTrack_IgnoresCase_WhenLookingForAFreeName()
    {
        var document = DocumentWithTracks("track", "TRACK 2");

        document.AddTrack(new UndoManager(), 0).ShouldBe("Track 3");
    }

    [Fact]
    public void AddTrack_FillsTheFirstGap()
    {
        var document = DocumentWithTracks("Track", "Track 3");

        document.AddTrack(new UndoManager(), 0).ShouldBe("Track 2");
    }
}
