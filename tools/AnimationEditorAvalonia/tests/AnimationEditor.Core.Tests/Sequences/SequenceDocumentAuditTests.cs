using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Document edits whose refusals and edge cases the first round of sequence tests left unpinned.</summary>
public class SequenceDocumentAuditTests
{
    private static string WriteAchj(string folder)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave { Name = "Walk", Frames = { new AnimationFrameSave { FrameLength = 0.5f } } });
        string path = Path.Combine(folder, "hero.achj");
        list.SaveJson(path);
        return path;
    }

    private static SequenceDocument Load(TestHelpers.TempDir dir, params string[] sequenceNames)
    {
        WriteAchj(dir.Path);
        var save = new SequenceListSave { Sources = { "hero.achj" } };
        foreach (string name in sequenceNames)
        {
            var sequence = new SequenceSave { Name = name };
            sequence.Tracks.Add(new SequenceTrackSave { Name = "Body" });
            sequence.Tracks.Add(new SequenceTrackSave { Name = "Arms" });
            save.Sequences.Add(sequence);
        }
        string path = Path.Combine(dir.Path, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenameSequence_EmptyOrBlank_IsRefusedWithNoUndoEntry(string typed)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();

        document.RenameSequence(undo, 0, typed).ShouldBe("A sequence needs a name.");

        document.Save.Sequences[0].Name.ShouldBe("Idle");
        undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameSequence_SurroundingSpaces_AreTrimmed_AndAnUnchangedNameRecordsNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();

        document.RenameSequence(undo, 0, "  Idle  ").ShouldBeNull();
        undo.CanUndo.ShouldBeFalse();
        document.RenameSequence(undo, 0, "  Charge ").ShouldBeNull();

        document.Save.Sequences[0].Name.ShouldBe("Charge");
        undo.UndoHistory.Single().Description.ShouldBe("Rename Sequence 'Idle' to 'Charge'");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RenameTrack_EmptyOrBlank_IsRefusedWithNoUndoEntry(string typed)
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();

        document.RenameTrack(undo, 0, 1, typed).ShouldBe("A track needs a name.");

        document.Save.Sequences[0].Tracks[1].Name.ShouldBe("Arms");
        undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void RenameTrack_SurroundingSpaces_AreTrimmed_AndAnUnchangedNameRecordsNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();

        document.RenameTrack(undo, 0, 1, " Arms ").ShouldBeNull();
        undo.CanUndo.ShouldBeFalse();
        document.RenameTrack(undo, 0, 1, " Legs ").ShouldBeNull();

        document.Save.Sequences[0].Tracks[1].Name.ShouldBe("Legs");
    }

    [Fact]
    public void AddSequence_WhenSequenceAndSequence2AreTaken_GivesSequence3()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Sequence", "Sequence 2");

        string added = document.AddSequence(new UndoManager());

        added.ShouldBe("Sequence 3");
    }

    [Fact]
    public void PlaceChain_OnTheNewTrackRowThreeTimes_NamesTheTracksWalkWalk2Walk3()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();

        for (int i = 0; i < 3; i++)
            document.PlaceChain(undo, 0, track: null, "hero.achj", "Walk", start: 0);

        document.Save.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Arms", "Walk", "Walk 2", "Walk 3" });
    }

    [Fact]
    public void PlaceChain_NegativeStart_IsClampedToZero()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");

        document.PlaceChain(new UndoManager(), 0, track: 0, "hero.achj", "Walk", start: -3);

        document.Save.Sequences[0].Tracks[0].Chains.Single().Start.ShouldBe(0);
    }

    [Fact]
    public void RemoveSource_ChainUsesItWithDifferentCase_IsStillRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();
        document.PlaceChain(undo, 0, 0, "HERO.achj", "Walk", 0);
        undo.Clear();

        string? error = document.RemoveSource(undo, "hero.achj");

        error.ShouldNotBeNull();
        error.ShouldContain("used by 1 chain");
        document.Save.Sources.ShouldBe(new[] { "hero.achj" });
        undo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void SaveToDisk_WhenTheFolderIsGone_ReportsSaveErrorInsteadOfThrowing_AndALaterSaveClearsIt()
    {
        using var dir = new TestHelpers.TempDir();
        string project = Path.Combine(dir.Path, "project");
        Directory.CreateDirectory(project);
        string seqj = Path.Combine(project, "boss.seqj");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(seqj);
        var document = SequenceDocument.Load(seqj);
        Directory.Delete(project, recursive: true);

        document.SaveToDisk();

        document.SaveError.ShouldNotBeNullOrEmpty();
        Directory.CreateDirectory(project);
        document.SaveToDisk();
        document.SaveError.ShouldBeNull();
        File.Exists(seqj).ShouldBeTrue();
    }

    [Fact]
    public void HiddenTracks_ToggleLeavesTheFileUndoHistoryAndSaveUntouched()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();
        byte[] before = File.ReadAllBytes(document.Path.FullPath);
        string json = document.Save.ToJsonString();

        document.HiddenTracks.Toggle("Idle", "Body");

        document.HiddenTracks.IsHidden("Idle", "Body").ShouldBeTrue();
        undo.CanUndo.ShouldBeFalse();
        File.ReadAllBytes(document.Path.FullPath).ShouldBe(before);
        document.Save.ToJsonString().ShouldBe(json);
    }

    [Fact]
    public void RenameTrack_CarriesTheHiddenState_AndUndoAndRedoCarryItBack()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();
        document.HiddenTracks.Toggle("Idle", "Body");

        document.RenameTrack(undo, 0, 0, "Torso");
        bool afterRename = document.HiddenTracks.IsHidden("Idle", "Torso");
        undo.Undo();
        bool afterUndo = document.HiddenTracks.IsHidden("Idle", "Body");
        undo.Redo();

        afterRename.ShouldBeTrue();
        afterUndo.ShouldBeTrue();
        document.HiddenTracks.Entries.Select(e => (e.Sequence, e.Track)).ShouldBe(new[] { ("Idle", "Torso") });
    }

    [Fact]
    public void RenameSequence_CarriesTheHiddenState_AndUndoCarriesItBack()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        var undo = new UndoManager();
        document.HiddenTracks.Toggle("Idle", "Body");

        document.RenameSequence(undo, 0, "Charge");
        bool afterRename = document.HiddenTracks.IsHidden("Charge", "Body");
        undo.Undo();

        afterRename.ShouldBeTrue();
        document.HiddenTracks.IsHidden("Idle", "Body").ShouldBeTrue();
    }

    [Fact]
    public void DeleteTrack_PrunesItsHiddenState()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Load(dir, "Idle");
        document.HiddenTracks.Toggle("Idle", "Body");
        document.HiddenTracks.Toggle("Idle", "Arms");

        document.DeleteTrack(new UndoManager(), 0, 0);

        document.HiddenTracks.Entries.Select(e => e.Track).ShouldBe(new[] { "Arms" });
    }
}
