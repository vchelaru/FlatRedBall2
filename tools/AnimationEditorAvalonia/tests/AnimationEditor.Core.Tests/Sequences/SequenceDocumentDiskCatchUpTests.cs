using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// A <c>.seqj</c> tab that was in the background while its file changed on disk (a git pull) catches up when it comes back,
/// by comparing the file's content with what the document last read or wrote.
/// </summary>
public class SequenceDocumentDiskCatchUpTests
{
    private static string WriteSeqj(string folder, params string[] sequenceNames)
    {
        string path = Path.Combine(folder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sequences.AddRange(sequenceNames.Select(name => new SequenceSave { Name = name }));
        save.Save(path);
        return path;
    }

    private static List<string> Names(SequenceDocument document) => document.Save.Sequences.Select(s => s.Name).ToList();

    [Fact]
    public void ReloadIfChangedOnDisk_FileRewrittenWhileAway_ReloadsAndRaisesChanged()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        int raised = 0;
        document.Changed += () => raised++;

        WriteSeqj(dir.Path, "Idle", "Die");
        var result = document.ReloadIfChangedOnDisk();

        result.Changed.ShouldBeTrue();
        result.Error.ShouldBeNull();
        raised.ShouldBe(1);
        Names(document).ShouldBe(new[] { "Idle", "Die" });
    }

    [Fact]
    public void ReloadIfChangedOnDisk_ForeignChange_SurvivesTheNextEditsSave()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        WriteSeqj(dir.Path, "Idle", "Die");

        document.ReloadIfChangedOnDisk();
        document.AddSequence(new UndoManager());

        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle", "Die", "Sequence" });
    }

    [Fact]
    public void ReloadIfChangedOnDisk_FileUntouched_DoesNothing()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        int raised = 0;
        document.Changed += () => raised++;

        var result = document.ReloadIfChangedOnDisk();

        result.ShouldBe(new SequenceReloadResult(false, null));
        raised.ShouldBe(0);
    }

    [Fact]
    public void ReloadIfChangedOnDisk_AfterOurOwnSave_IsNotAChange()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        document.AddSequence(new UndoManager());
        int raised = 0;
        document.Changed += () => raised++;

        var result = document.ReloadIfChangedOnDisk();

        result.ShouldBe(new SequenceReloadResult(false, null));
        raised.ShouldBe(0);
        Names(document).ShouldBe(new[] { "Idle", "Sequence" });
    }

    [Fact]
    public void ReloadIfChangedOnDisk_SaveThatFailedToWrite_KeepsTheEditsWhileTheFileIsUntouched()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        document.SaveRunner = (_, _) => throw new IOException("disk full");
        document.AddSequence(new UndoManager());
        document.SaveError.ShouldNotBeNull();

        var result = document.ReloadIfChangedOnDisk();

        result.Changed.ShouldBeFalse();
        Names(document).ShouldBe(new[] { "Idle", "Sequence" });
    }

    [Fact]
    public void ReloadIfChangedOnDisk_IdenticalRewrite_IsNotAChange()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        int raised = 0;
        document.Changed += () => raised++;

        File.WriteAllText(path, File.ReadAllText(path));
        File.SetLastWriteTimeUtc(path, System.DateTime.UtcNow.AddMinutes(5));
        var result = document.ReloadIfChangedOnDisk();

        result.ShouldBe(new SequenceReloadResult(false, null));
        raised.ShouldBe(0);
    }

    [Fact]
    public void ReloadIfChangedOnDisk_InvalidJson_KeepsTheDocument_PausesSaving_AndRecoversOnTheNextValidFile()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        const string foreign = "{ \"sequences\": [ { \"name\": \"Half";
        File.WriteAllText(path, foreign);

        var failed = document.ReloadIfChangedOnDisk();

        failed.Error.ShouldNotBeNull();
        failed.Changed.ShouldBeFalse();
        Names(document).ShouldBe(new[] { "Idle" });
        document.StaleOnDiskReason.ShouldNotBeNull();
        document.AddSequence(new UndoManager());
        File.ReadAllText(path).ShouldBe(foreign, "saving is paused rather than destroy the other program's file");

        // Still the same bad content: nothing new to try, and still paused.
        document.ReloadIfChangedOnDisk().Changed.ShouldBeFalse();
        document.StaleOnDiskReason.ShouldNotBeNull();

        WriteSeqj(dir.Path, "Idle", "Die");
        var recovered = document.ReloadIfChangedOnDisk();

        recovered.Changed.ShouldBeTrue();
        recovered.Error.ShouldBeNull();
        document.StaleOnDiskReason.ShouldBeNull();
        Names(document).ShouldBe(new[] { "Idle", "Die" });
    }

    [Fact]
    public void ReloadIfChangedOnDisk_InvalidThenRestoredToTheOldContent_ClearsTheStaleState()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        string original = File.ReadAllText(path);
        var document = SequenceDocument.Load(path);
        File.WriteAllText(path, "{ nope");
        document.ReloadIfChangedOnDisk().Error.ShouldNotBeNull();

        File.WriteAllText(path, original);
        var result = document.ReloadIfChangedOnDisk();

        result.Changed.ShouldBeFalse("the data equals what is in memory");
        result.Error.ShouldBeNull();
        document.StaleOnDiskReason.ShouldBeNull();
    }

    [Fact]
    public void ReloadIfChangedOnDisk_FileDeleted_ReportsItAndKeepsTheDocument()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        int raised = 0;
        document.Changed += () => raised++;

        File.Delete(path);
        var result = document.ReloadIfChangedOnDisk();

        result.Deleted.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.Changed.ShouldBeFalse();
        raised.ShouldBe(0);
        Names(document).ShouldBe(new[] { "Idle" });
        document.StaleOnDiskReason.ShouldBeNull("a deleted file does not pause saving, as on the live path");
    }

    [Fact]
    public void ReloadFromDisk_ThatChangesTheData_ClearsASaveErrorTheDiscardedEditsCaused()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        document.SaveRunner = (_, _) => throw new IOException("read-only");
        document.AddSequence(new UndoManager());
        document.SaveError.ShouldNotBeNull();

        WriteSeqj(dir.Path, "Idle", "Die");
        document.ReloadFromDisk().Changed.ShouldBeTrue();

        document.SaveError.ShouldBeNull("memory now equals the file again");
    }

    [Fact]
    public void ReloadFromDisk_RecordsWhatItRead_SoALaterCatchUpDoesNotReadItAgain()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        WriteSeqj(dir.Path, "Idle", "Die");
        document.ReloadFromDisk().Changed.ShouldBeTrue();
        int raised = 0;
        document.Changed += () => raised++;

        document.ReloadIfChangedOnDisk().ShouldBe(new SequenceReloadResult(false, null));

        raised.ShouldBe(0);
    }
}
