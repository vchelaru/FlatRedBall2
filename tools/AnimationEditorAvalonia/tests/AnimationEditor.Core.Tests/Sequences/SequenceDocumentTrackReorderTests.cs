using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary><see cref="SequenceDocument.MoveTrack"/>: one undo entry, the saved order and Z, and the selection that follows.</summary>
public class SequenceDocumentTrackReorderTests
{
    // Sequence 0 has tracks Back (Z 0), Mid (Z 1), Front (Z 2); Back carries a field this editor does not know.
    private static (SequenceDocument Document, UndoManager Undo, string Path) Open(TestHelpers.TempDir dir)
    {
        string path = Path.Combine(dir.Path, "boss.seqj");
        File.WriteAllText(path, """
            { "version": 1, "sources": [],
              "sequences": [ { "name": "Idle", "tracks": [
                { "name": "Back", "z": 0, "rig": 7, "chains": [] },
                { "name": "Mid", "z": 1, "chains": [] },
                { "name": "Front", "z": 2, "chains": [] } ] } ] }
            """);
        return (SequenceDocument.Load(path), new UndoManager(), path);
    }

    private static string[] Names(SequenceDocument document) => document.Save.Sequences[0].Tracks.Select(t => t.Name).ToArray();

    [Fact]
    public void MoveTrack_FirstToLast_IsOneUndoEntry_ThatSavesTheNewOrderAndZ()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo, path) = Open(dir);

        int? now = document.MoveTrack(undo, 0, from: 0, slot: 3);

        now.ShouldBe(2);
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Move Track 'Back'" });
        Names(document).ShouldBe(new[] { "Mid", "Front", "Back" });
        var saved = SequenceListSave.FromFile(path).Sequences[0].Tracks;
        saved.Select(t => t.Name).ShouldBe(new[] { "Mid", "Front", "Back" });
        saved.Select(t => t.Z).ShouldBe(new[] { 1, 2, 2 });
    }

    [Fact]
    public void MoveTrack_UndoAndRedo_RestoreTheOrderAndZ()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo, path) = Open(dir);
        document.MoveTrack(undo, 0, from: 2, slot: 0);

        undo.Undo();
        Names(document).ShouldBe(new[] { "Back", "Mid", "Front" });
        document.Save.Sequences[0].Tracks.Select(t => t.Z).ShouldBe(new[] { 0, 1, 2 });
        undo.Redo();

        Names(document).ShouldBe(new[] { "Front", "Back", "Mid" });
        SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(t => t.Z).ShouldBe(new[] { 0, 0, 1 });
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public void MoveTrack_DroppedNextToItself_RecordsNothing(int from, int slot)
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo, _) = Open(dir);

        document.MoveTrack(undo, 0, from, slot).ShouldBeNull();

        undo.UndoHistory.ShouldBeEmpty();
        document.TakeTrackSelection().ShouldBeNull();
    }

    [Fact]
    public void MoveTrack_SelectsTheMovedTrackAfterDoAndUndoAndRedo_OnceEach()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo, _) = Open(dir);

        document.MoveTrack(undo, 0, from: 0, slot: 3);
        document.TakeTrackSelection().ShouldBe(2);
        document.TakeTrackSelection().ShouldBeNull();

        undo.Undo();
        document.TakeTrackSelection().ShouldBe(0);

        undo.Redo();
        document.TakeTrackSelection().ShouldBe(2);
    }

    [Fact]
    public void MoveTrack_KeepsUnknownFieldsOnTheMovedTrack_AcrossSaveAndUndo()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo, path) = Open(dir);

        document.MoveTrack(undo, 0, from: 0, slot: 3);
        File.ReadAllText(path).ShouldContain("\"rig\": 7");
        undo.Undo();

        File.ReadAllText(path).ShouldContain("\"rig\": 7");
    }

    [Fact]
    public void MoveTrack_SelectsTheSequenceItChanged()
    {
        using var dir = new TestHelpers.TempDir();
        var (document, undo, _) = Open(dir);
        document.AddSequence(undo);
        document.SelectedSequence = 1;

        document.MoveTrack(undo, 0, from: 0, slot: 3);

        document.SelectedSequence.ShouldBe(0);
    }
}
