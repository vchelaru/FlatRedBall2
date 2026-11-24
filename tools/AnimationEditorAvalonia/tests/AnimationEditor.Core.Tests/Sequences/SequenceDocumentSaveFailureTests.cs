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
/// A failed write of a <c>.seqj</c> is reported the way the <c>.achx</c> path reports it (an event the window turns into
/// an "Auto save failed" toast), and the document knows it holds edits the file lacks.
/// </summary>
public class SequenceDocumentSaveFailureTests
{
    private static string WriteSeqj(string folder, params string[] sequenceNames)
    {
        string path = Path.Combine(folder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sequences.AddRange(sequenceNames.Select(name => new SequenceSave { Name = name }));
        save.Save(path);
        return path;
    }

    [Fact]
    public void FailedWrite_RaisesSaveFailed_AndLeavesUnsavedEdits_UntilARetrySucceeds()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        var failures = new List<string>();
        document.SaveFailed += failures.Add;
        document.SaveRunner = (_, _) => throw new IOException("disk full");

        document.AddSequence(new UndoManager());

        failures.ShouldBe(new[] { "disk full" });
        document.SaveError.ShouldBe("disk full");
        document.HasUnsavedEdits.ShouldBeTrue();

        document.SaveRunner = static (_, write) => write();
        document.SaveToDisk();

        document.SaveError.ShouldBeNull();
        document.HasUnsavedEdits.ShouldBeFalse();
        failures.Count.ShouldBe(1);
        SequenceListSave.FromFile(path).Sequences.Count.ShouldBe(2);
    }

    [Fact]
    public void SavingPausedByAnUnreadableFile_RaisesSaveFailed_WithTheReason()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        File.WriteAllText(path, "{ \"sequences\": [ { \"name\": \"Half");
        document.ReloadIfChangedOnDisk();
        var failures = new List<string>();
        document.SaveFailed += failures.Add;

        document.AddSequence(new UndoManager());

        failures.Count.ShouldBe(1);
        failures[0].ShouldContain("changed on disk and couldn't be reloaded");
        document.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public void SuccessfulWrite_RaisesNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = SequenceDocument.Load(WriteSeqj(dir.Path, "Idle"));
        int raised = 0;
        document.SaveFailed += _ => raised++;

        document.AddSequence(new UndoManager());

        raised.ShouldBe(0);
        document.HasUnsavedEdits.ShouldBeFalse();
    }

    // A document holding "Sequence", whose save failed, and a file that then gained "Die" outside.
    private static SequenceDocument FailASaveThenChangeTheFile(string folder, out string path)
    {
        path = WriteSeqj(folder, "Idle");
        var document = SequenceDocument.Load(path);
        document.SaveRunner = (_, _) => throw new IOException("disk full");
        document.AddSequence(new UndoManager());
        document.SaveRunner = static (_, write) => write();
        WriteSeqj(folder, "Idle", "Die");
        return document;
    }

    private static IEnumerable<string> Names(SequenceListSave save) => save.Sequences.Select(sequence => sequence.Name);

    [Fact]
    public void ReloadIfChangedOnDisk_AfterAFailedWrite_KeepsTheEditsAndTheFile_UntilAReload()
    {
        using var dir = new TestHelpers.TempDir();
        var document = FailASaveThenChangeTheFile(dir.Path, out string path);
        var failures = new List<string>();
        document.SaveFailed += failures.Add;

        document.ReloadIfChangedOnDisk().ShouldBe(new SequenceReloadResult(false, null));

        Names(document.Save).ShouldBe(new[] { "Idle", "Sequence" });
        failures.Single().ShouldContain("\"boss.seqj\" changed on disk");
        document.SaveToDisk();
        Names(SequenceListSave.FromFile(path)).ShouldBe(new[] { "Idle", "Die" }, "the save must not overwrite the newer file");
        failures.Count.ShouldBe(2, "a refused save says so");

        document.ReloadFromDisk().Changed.ShouldBeTrue();
        document.HasUnsavedEdits.ShouldBeFalse();
        document.AddSequence(new UndoManager());
        Names(SequenceListSave.FromFile(path)).ShouldBe(new[] { "Idle", "Die", "Sequence" });
    }

    [Fact]
    public void SaveAs_AfterAFailedWriteAndAnOutsideChange_WritesTheEditsElsewhereAndResumesSaving()
    {
        using var dir = new TestHelpers.TempDir();
        var document = FailASaveThenChangeTheFile(dir.Path, out string path);
        document.ReloadIfChangedOnDisk();
        string copy = Path.Combine(dir.Path, "copy.seqj");

        document.SaveAs(copy).ShouldBeNull();
        document.AddSequence(new UndoManager());

        Names(SequenceListSave.FromFile(copy)).ShouldBe(new[] { "Idle", "Sequence", "Sequence 2" });
        Names(SequenceListSave.FromFile(path)).ShouldBe(new[] { "Idle", "Die" });
    }

    [Fact]
    public void ReloadFromDisk_OfAFileThatHoldsTheUnsavedEdits_LeavesNothingUnsaved()
    {
        using var dir = new TestHelpers.TempDir();
        var document = FailASaveThenChangeTheFile(dir.Path, out string path);
        document.Save.Save(path);

        document.ReloadFromDisk();

        document.HasUnsavedEdits.ShouldBeFalse();
    }
}
