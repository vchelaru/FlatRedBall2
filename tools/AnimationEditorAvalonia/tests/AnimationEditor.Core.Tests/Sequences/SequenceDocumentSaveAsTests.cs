using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// File &gt; Save As on a <c>.seqj</c>: the document moves to the new file. Sources are stored relative to the file, so
/// the relative paths are rewritten to keep pointing at the same files; the old file is never written.
/// </summary>
public class SequenceDocumentSaveAsTests
{
    private static void WriteAchx(string path, params string[] chains)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (string chain in chains) list.AnimationChains.Add(new AnimationChainSave { Name = chain });
        list.Save(path);
    }

    // chars/hero.achx and enemy.achx beside boss.seqj; one chain of each on the timeline.
    private static SequenceDocument OpenBoss(string folder, string seqjName = "boss.seqj", string subfolder = "")
    {
        string root = System.IO.Path.Combine(folder, subfolder);
        WriteAchx(System.IO.Path.Combine(root, "chars", "hero.achx"), "Walk", "Run");
        WriteAchx(System.IO.Path.Combine(root, "enemy.achx"), "Bite");
        var save = new SequenceListSave();
        save.Sources.AddRange(new[] { "chars/hero.achx", "enemy.achx" });
        var track = new SequenceTrackSave { Name = "T" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "chars/hero.achx", Chain = "Walk", Loops = 1 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "enemy.achx", Chain = "Bite", Start = 1, Loops = 1 });
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        string path = System.IO.Path.Combine(root, seqjName);
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static string[] Labels(UndoManager undo) => undo.UndoHistory.Select(c => c.Description).ToArray();

    private static string[] ChainSources(SequenceListSave save) =>
        save.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains).Select(c => c.Source).ToArray();

    [Fact]
    public void SaveAs_ToANestedFolder_RewritesSourcesSoTheyStillResolve_AndWritesTheNewFile()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        string target = System.IO.Path.Combine(dir.Path, "levels", "deep", "boss2.seqj");

        document.SaveAs(target).ShouldBeNull();

        document.Path.ShouldBe(new FilePath(target));
        document.Save.Sources.ShouldBe(new[] { "../../chars/hero.achx", "../../enemy.achx" });
        ChainSources(document.Save).ShouldBe(new[] { "../../chars/hero.achx", "../../enemy.achx" });
        var saved = SequenceListSave.FromFile(target);
        saved.Sources.ShouldBe(new[] { "../../chars/hero.achx", "../../enemy.achx" });
        ChainSources(saved).ShouldBe(new[] { "../../chars/hero.achx", "../../enemy.achx" });
        document.SourceChains("../../chars/hero.achx")!.AnimationChains.Select(c => c.Name).ShouldBe(new[] { "Walk", "Run" });
        document.IsSourceMissing("../../enemy.achx").ShouldBeFalse();
        document.FindChain(document.Save.Sequences[0].Tracks[0].Chains[0]).ShouldNotBeNull();
    }

    [Fact]
    public void SaveAs_ToAParentFolder_PrefixesTheSubfolder()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path, subfolder: "game");
        string target = System.IO.Path.Combine(dir.Path, "boss2.seqj");

        document.SaveAs(target).ShouldBeNull();

        document.Save.Sources.ShouldBe(new[] { "game/chars/hero.achx", "game/enemy.achx" });
        document.IsSourceMissing("game/chars/hero.achx").ShouldBeFalse();
        SequenceListSave.FromFile(target).Sources.ShouldBe(new[] { "game/chars/hero.achx", "game/enemy.achx" });
    }

    [Fact]
    public void SaveAs_InTheSameFolder_KeepsTheSourcesAsWritten()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);

        document.SaveAs(System.IO.Path.Combine(dir.Path, "copy.seqj")).ShouldBeNull();

        document.Save.Sources.ShouldBe(new[] { "chars/hero.achx", "enemy.achx" });
        File.Exists(System.IO.Path.Combine(dir.Path, "copy.seqj")).ShouldBeTrue();
    }

    [Fact]
    public void SaveAs_NeverWritesTheOldFile_NotNowAndNotOnLaterEdits()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        string old = document.Path.FullPath;
        byte[] before = File.ReadAllBytes(old);
        var writes = new List<string>();
        document.SaveRunner = (path, write) => { writes.Add(path); write(); };
        string target = System.IO.Path.Combine(dir.Path, "sub", "boss2.seqj");

        document.SaveAs(target).ShouldBeNull();
        document.AddSequence(new UndoManager());

        File.ReadAllBytes(old).ShouldBe(before);
        writes.Select(path => new FilePath(path)).ShouldAllBe(path => path == new FilePath(target));
        SequenceListSave.FromFile(target).Sequences.Count.ShouldBe(2);
    }

    [Fact]
    public void SaveAs_KeepsTheUndoHistory_AndUndoAfterItKeepsEverySourceResolving()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        var undo = new UndoManager();
        document.RenameSequence(undo, 0, "Strike");
        document.AddSequence(undo);
        string target = System.IO.Path.Combine(dir.Path, "levels", "boss2.seqj");

        document.SaveAs(target).ShouldBeNull();

        Labels(undo).Length.ShouldBe(2);
        undo.Undo();
        undo.Undo();
        document.Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "Attack" });
        document.Save.Sources.ShouldBe(new[] { "../chars/hero.achx", "../enemy.achx" });
        ChainSources(document.Save).ShouldBe(new[] { "../chars/hero.achx", "../enemy.achx" });
        document.IsSourceMissing("../chars/hero.achx").ShouldBeFalse();
        SequenceListSave.FromFile(target).Sources.ShouldBe(new[] { "../chars/hero.achx", "../enemy.achx" });

        undo.Redo();
        undo.Redo();
        document.Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "Strike", "Sequence" });
        document.Save.Sources.ShouldBe(new[] { "../chars/hero.achx", "../enemy.achx" });
    }

    [Fact]
    public void SaveAs_AfterAnEditMadeBeforeIt_UndoRebasesAnAddedSourceToo()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        WriteAchx(System.IO.Path.Combine(dir.Path, "extra.achx"), "Spin");
        var undo = new UndoManager();
        document.AddSources(undo, new[] { System.IO.Path.Combine(dir.Path, "extra.achx") });
        string target = System.IO.Path.Combine(dir.Path, "levels", "boss2.seqj");
        document.SaveAs(target).ShouldBeNull();

        undo.Undo();
        undo.Redo();

        document.Save.Sources.ShouldBe(new[] { "../chars/hero.achx", "../enemy.achx", "../extra.achx" });
        document.IsSourceMissing("../extra.achx").ShouldBeFalse();
    }

    [Fact]
    public void SaveAs_RaisesChanged_AndKeepsTheSelectedSequence()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        document.AddSequence(new UndoManager());
        document.SelectedSequence = 1;
        int changes = 0;
        document.Changed += () => changes++;

        document.SaveAs(System.IO.Path.Combine(dir.Path, "sub", "boss2.seqj")).ShouldBeNull();

        changes.ShouldBeGreaterThan(0);
        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void SaveAs_ToTheCurrentPath_JustSaves()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        var writes = new List<string>();
        document.SaveRunner = (path, write) => { writes.Add(path); write(); };

        document.SaveAs(document.Path.FullPath).ShouldBeNull();

        document.Path.ShouldBe(new FilePath(document.Path.FullPath));
        writes.Count.ShouldBe(1);
    }

    [Fact]
    public void SaveAs_ToAFileThatIsNotSeqj_IsRefused_AndChangesNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        string old = document.Path.FullPath;
        string target = System.IO.Path.Combine(dir.Path, "boss.txt");

        string? error = document.SaveAs(target);

        error.ShouldNotBeNull();
        error.ShouldContain(".seqj");
        File.Exists(target).ShouldBeFalse();
        document.Path.ShouldBe(new FilePath(old));
        document.Save.Sources.ShouldBe(new[] { "chars/hero.achx", "enemy.achx" });
    }

    [Fact]
    public void SaveAs_WhenTheTargetCannotBeWritten_ReportsIt_AndKeepsTheDocumentWhereItWas()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        string old = document.Path.FullPath;
        document.SaveRunner = (_, _) => throw new IOException("disk full");

        string? error = document.SaveAs(System.IO.Path.Combine(dir.Path, "sub", "boss2.seqj"));

        error.ShouldNotBeNull();
        error.ShouldContain("disk full");
        document.Path.ShouldBe(new FilePath(old));
        document.Save.Sources.ShouldBe(new[] { "chars/hero.achx", "enemy.achx" });
    }

    [Fact]
    public void SaveAs_OnAnotherDrive_IsRefusedBeforeAnythingIsWritten()
    {
        if (!OperatingSystem.IsWindows()) return;   // a drive letter only exists on Windows
        using var dir = new TestHelpers.TempDir();
        var document = OpenBoss(dir.Path);
        string old = document.Path.FullPath;
        byte[] before = File.ReadAllBytes(old);
        string drive = System.IO.Path.GetPathRoot(dir.Path)![0] == 'Q' ? "R" : "Q";

        string? error = document.SaveAs($@"{drive}:\elsewhere\boss.seqj");

        error.ShouldNotBeNull();
        error.ShouldContain("hero.achx");
        error.ShouldContain("drive");
        document.Path.ShouldBe(new FilePath(old));
        File.ReadAllBytes(old).ShouldBe(before);
        document.Save.Sources.ShouldBe(new[] { "chars/hero.achx", "enemy.achx" });
    }
}
