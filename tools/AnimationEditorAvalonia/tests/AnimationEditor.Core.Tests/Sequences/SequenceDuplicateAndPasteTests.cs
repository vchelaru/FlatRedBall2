using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Duplicating a sequence in place, and pasting a copied one into the same file or another.</summary>
public class SequenceDuplicateAndPasteTests
{
    private static SequenceSave Sequence(string name, params string[] sources)
    {
        var track = new SequenceTrackSave { Name = "Body", X = 4, Y = -1, FlipHorizontal = true };
        foreach (string source in sources)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = "Walk", Start = 0.25, Loops = 3 });
        var sequence = new SequenceSave { Name = name, Loop = true };
        sequence.Tracks.Add(track);
        return sequence;
    }

    private static string WriteSeqj(string folder, string fileName, SequenceListSave save)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        save.Save(path);
        return path;
    }

    private static SequenceListSave SaveWith(string[] sources, params SequenceSave[] sequences)
    {
        var save = new SequenceListSave();
        save.Sources.AddRange(sources);
        save.Sequences.AddRange(sequences);
        return save;
    }

    private static string[] Names(SequenceDocument document) => document.Save.Sequences.Select(s => s.Name).ToArray();

    private static string ToJson(SequenceSave sequence) => SequenceClipboard.Write(sequence, "C:/x");

    [Fact]
    public void DuplicateSequence_InsertsADeepCopyRightAfterTheOriginal_WithTheNextFreeName_AndSelectsIt()
    {
        using var dir = new TestHelpers.TempDir();
        var first = Sequence("Idle", "hero.achx");
        first.UnknownProperties["note"] = "keep";
        first.Tracks[0].Visible = false;
        first.Tracks[0].UnknownProperties["tag"] = "boss";
        first.Tracks[0].Chains[0].UnknownProperties["tint"] = new JsonObject { ["r"] = 1 };
        string path = WriteSeqj(dir.Path, "boss.seqj", SaveWith(new[] { "hero.achx" }, first, Sequence("Attack", "hero.achx")));
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();

        string name = document.DuplicateSequences(undo, new[] { 0 }).Single();

        name.ShouldBe("Idle 2");
        Names(document).ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
        document.SelectedSequence.ShouldBe(1);
        var original = document.Save.Sequences[0];
        var copy = document.Save.Sequences[1];
        copy.Name = "Idle";
        ToJson(copy).ShouldBe(ToJson(original));
        copy.Tracks[0].Chains[0].Start = 9;
        original.Tracks[0].Chains[0].Start.ShouldBe(0.25);
        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
    }

    [Fact]
    public void DuplicateSequence_IsOneUndoEntry_AndUndoRemovesTheCopyAndReselectsTheOriginal()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "boss.seqj", SaveWith(new string[0], Sequence("Idle"), Sequence("Attack")));
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();

        document.DuplicateSequences(undo, new[] { 0 });

        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Duplicate Sequence 'Idle'" });
        undo.Undo();
        Names(document).ShouldBe(new[] { "Idle", "Attack" });
        document.SelectedSequence.ShouldBe(0);
        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Idle", "Attack" });
        undo.Redo();
        Names(document).ShouldBe(new[] { "Idle", "Idle 2", "Attack" });
        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void DuplicateSequence_OfANumberedName_TakesTheNextFreeNumber()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "boss.seqj", SaveWith(new string[0], Sequence("Idle"), Sequence("Idle 2")));
        var document = SequenceDocument.Load(path);

        document.DuplicateSequences(new UndoManager(), new[] { 1 }).ShouldBe(new[] { "Idle 3" });

        Names(document).ShouldBe(new[] { "Idle", "Idle 2", "Idle 3" });
    }

    [Fact]
    public void PasteSequence_InTheSameFile_InsertsAfterTheSelectedOne_WithAFreeName_AsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "boss.seqj", SaveWith(new[] { "hero.achx" }, Sequence("Idle", "hero.achx"), Sequence("Attack", "hero.achx")));
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        SequenceClipboard.TryRead(document.CopySequences(new[] { 1 }), out var content, out _).ShouldBeTrue();
        document.SelectedSequence = 0;

        var result = document.PasteSequences(undo, content!);

        result.Names.ShouldBe(new[] { "Attack 2" });
        result.Errors.ShouldBeEmpty();
        Names(document).ShouldBe(new[] { "Idle", "Attack 2", "Attack" });
        document.SelectedSequence.ShouldBe(1);
        document.Save.Sources.ShouldBe(new[] { "hero.achx" });
        document.Save.Sequences[1].Tracks[0].Chains[0].Source.ShouldBe("hero.achx");
        undo.UndoHistory.Select(c => c.Description).ShouldBe(new[] { "Paste Sequence 'Attack 2'" });
        undo.Undo();
        Names(document).ShouldBe(new[] { "Idle", "Attack" });
    }

    [Fact]
    public void PasteSequence_WithNothingSelected_GoesAtTheEnd()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "boss.seqj", SaveWith(new string[0], Sequence("Idle"), Sequence("Attack")));
        var document = SequenceDocument.Load(path);
        SequenceClipboard.TryRead(document.CopySequences(new[] { 0 }), out var content, out _).ShouldBeTrue();
        document.SelectedSequence = -1;

        document.PasteSequences(new UndoManager(), content!);

        Names(document).ShouldBe(new[] { "Idle", "Attack", "Idle 2" });
        document.SelectedSequence.ShouldBe(2);
    }

    [Fact]
    public void PasteSequence_IntoAnotherFile_AddsItsSourcesOnce_RelativeToTheDestination_InTheSameUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        string aPath = WriteSeqj(Path.Combine(dir.Path, "a"), "boss.seqj",
            SaveWith(new[] { "hero.achx", "../shared/fx.achj" }, Sequence("Idle", "hero.achx", "../shared/fx.achj", "hero.achx")));
        string bPath = WriteSeqj(Path.Combine(dir.Path, "b", "deep"), "other.seqj", SaveWith(new string[0], Sequence("Run")));
        var a = SequenceDocument.Load(aPath);
        var b = SequenceDocument.Load(bPath);
        var undo = new UndoManager();
        SequenceClipboard.TryRead(a.CopySequences(new[] { 0 }), out var content, out _).ShouldBeTrue();

        var result = b.PasteSequences(undo, content!);
        b.PasteSequences(undo, content!);

        result.Errors.ShouldBeEmpty();
        b.Save.Sources.ShouldBe(new[] { "../../a/hero.achx", "../../shared/fx.achj" });
        var chains = b.Save.Sequences[1].Tracks[0].Chains;
        chains.Select(c => c.Source).ShouldBe(new[] { "../../a/hero.achx", "../../shared/fx.achj", "../../a/hero.achx" });
        undo.UndoHistory.Count.ShouldBe(2);
        undo.Undo();
        undo.Undo();
        b.Save.Sources.ShouldBeEmpty();
        Names(b).ShouldBe(new[] { "Run" });
        SequenceListSave.FromFile(bPath).Sources.ShouldBeEmpty();
    }

    [Fact]
    public void PasteSequence_IntoAFileThatAlreadyListsTheSourceInOtherCase_DoesNotAddItAgain()
    {
        using var dir = new TestHelpers.TempDir();
        string aPath = WriteSeqj(Path.Combine(dir.Path, "a"), "boss.seqj", SaveWith(new[] { "hero.achx" }, Sequence("Idle", "hero.achx")));
        string bPath = WriteSeqj(Path.Combine(dir.Path, "b"), "other.seqj", SaveWith(new[] { "../A/HERO.achx" }, Sequence("Run")));
        var b = SequenceDocument.Load(bPath);
        SequenceClipboard.TryRead(SequenceDocument.Load(aPath).CopySequences(new[] { 0 }), out var content, out _).ShouldBeTrue();

        b.PasteSequences(new UndoManager(), content!);

        b.Save.Sources.ShouldBe(new[] { "../A/HERO.achx" });
        b.Save.Sequences[1].Tracks[0].Chains[0].Source.ShouldBe("../A/HERO.achx");
    }

    [Fact]
    public void PasteSequence_WithAMissingSource_KeepsTheReferenceAndListsTheSource()
    {
        using var dir = new TestHelpers.TempDir();
        string aPath = WriteSeqj(Path.Combine(dir.Path, "a"), "boss.seqj", SaveWith(new[] { "gone.achx" }, Sequence("Idle", "gone.achx")));
        string bPath = WriteSeqj(Path.Combine(dir.Path, "b"), "other.seqj", SaveWith(new string[0]));
        var b = SequenceDocument.Load(bPath);
        SequenceClipboard.TryRead(SequenceDocument.Load(aPath).CopySequences(new[] { 0 }), out var content, out _).ShouldBeTrue();

        var result = b.PasteSequences(new UndoManager(), content!);

        result.Errors.ShouldBeEmpty();
        b.Save.Sources.ShouldBe(new[] { "../a/gone.achx" });
        b.IsSourceMissing("../a/gone.achx").ShouldBeTrue();
        b.Save.Sequences.Single().Tracks[0].Chains[0].Source.ShouldBe("../a/gone.achx");
    }

    [Fact]
    public void PasteSequence_WithASourceThatIsTheDestinationItselfOrASeqj_StillPastes_AndDropsThoseSourcesWithAMessage()
    {
        using var dir = new TestHelpers.TempDir();
        string bPath = WriteSeqj(Path.Combine(dir.Path, "b"), "other.seqj", SaveWith(new string[0], Sequence("Run")));
        var b = SequenceDocument.Load(bPath);
        var pasted = Sequence("Idle", "hero.achx");
        string folderA = Path.Combine(dir.Path, "a").Replace('\\', '/');
        pasted.Tracks[0].Chains[0].Source = bPath.Replace('\\', '/');
        pasted.Tracks[0].Chains.Add(new SequenceChainReferenceSave { Source = folderA + "/boss.seqj", Chain = "X" });
        pasted.Tracks[0].Chains.Add(new SequenceChainReferenceSave { Source = folderA + "/ok.achx", Chain = "Y" });
        var content = new SequenceClipboardContent(new[] { pasted }, new[] { bPath.Replace('\\', '/'), folderA + "/boss.seqj", folderA + "/ok.achx" });

        var result = b.PasteSequences(new UndoManager(), content);

        Names(b).ShouldBe(new[] { "Run", "Idle" });
        b.Save.Sources.ShouldBe(new[] { "../a/ok.achx" });
        result.Errors.Count.ShouldBe(2);
        result.Errors[0].ShouldContain("other.seqj");
        result.Errors[1].ShouldContain("boss.seqj");
    }
}
