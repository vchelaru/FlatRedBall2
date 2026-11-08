using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The open <c>.seqj</c> following its own file when another program rewrites it (git pull, another editor, an agent).</summary>
public class SequenceDocumentReloadFromDiskTests
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
    public void ReloadFromDisk_FileRewritten_ReplacesTheDataAndRaisesChanged()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle", "Attack");
        var document = SequenceDocument.Load(path);
        int raised = 0;
        document.Changed += () => raised++;

        WriteSeqj(dir.Path, "Idle", "Attack", "Die");
        var result = document.ReloadFromDisk();

        result.Error.ShouldBeNull();
        result.Changed.ShouldBeTrue();
        raised.ShouldBe(1);
        Names(document).ShouldBe(new[] { "Idle", "Attack", "Die" });
    }

    [Fact]
    public void ReloadFromDisk_KeepsTheSelectedSequenceByName_WhenItMoved()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle", "Attack");
        var document = SequenceDocument.Load(path);
        document.SelectedSequence = 1;

        WriteSeqj(dir.Path, "Die", "Idle", "Attack");
        document.ReloadFromDisk();

        document.SelectedSequence.ShouldBe(2);
    }

    [Fact]
    public void ReloadFromDisk_SelectedSequenceGone_ClampsToTheLastOne()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle", "Attack", "Die");
        var document = SequenceDocument.Load(path);
        document.SelectedSequence = 2;

        WriteSeqj(dir.Path, "Idle", "Attack");
        document.ReloadFromDisk();

        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void ReloadFromDisk_SameContentAsMemory_ReportsNoChangeAndRaisesNothing()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        int raised = 0;
        document.Changed += () => raised++;

        var result = document.ReloadFromDisk();

        result.Changed.ShouldBeFalse();
        result.Error.ShouldBeNull();
        raised.ShouldBe(0);
    }

    [Fact]
    public void ReloadFromDisk_DoesNotWriteTheFileBack()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        WriteSeqj(dir.Path, "Idle", "Attack");
        var pinned = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, pinned);

        document.ReloadFromDisk();

        File.GetLastWriteTimeUtc(path).ShouldBe(pinned, "reading the file must not save it");
    }

    [Fact]
    public void ReloadFromDisk_InvalidJson_KeepsTheDocumentReportsWhy_AndRecoversOnTheNextGoodRead()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle", "Attack");
        var document = SequenceDocument.Load(path);
        int raised = 0;
        document.Changed += () => raised++;

        File.WriteAllText(path, "{ \"sequences\": [ { \"name\": \"Half");
        var failed = document.ReloadFromDisk();

        failed.Error.ShouldNotBeNull();
        failed.Changed.ShouldBeFalse();
        raised.ShouldBe(0);
        Names(document).ShouldBe(new[] { "Idle", "Attack" });
        document.StaleOnDiskReason.ShouldNotBeNull();

        WriteSeqj(dir.Path, "Idle", "Attack", "Die");
        var recovered = document.ReloadFromDisk();

        recovered.Error.ShouldBeNull();
        recovered.Changed.ShouldBeTrue();
        document.StaleOnDiskReason.ShouldBeNull();
        Names(document).ShouldBe(new[] { "Idle", "Attack", "Die" });
    }

    [Fact]
    public void ReloadFromDisk_FileMissing_KeepsTheDocument()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);

        File.Delete(path);
        var result = document.ReloadFromDisk();

        result.Error.ShouldNotBeNull();
        Names(document).ShouldBe(new[] { "Idle" });
    }

    [Fact]
    public void Edit_WhileTheFileCouldNotBeReloaded_DoesNotOverwriteTheFile()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        const string foreign = "{ not a sequence file yet";
        File.WriteAllText(path, foreign);
        document.ReloadFromDisk().Error.ShouldNotBeNull();

        document.AddSequence(new UndoManager());

        File.ReadAllText(path).ShouldBe(foreign, "saving would destroy what the other program wrote");
        document.SaveError.ShouldNotBeNull();
    }

    [Fact]
    public void SaveToDisk_GoesThroughTheSaveRunner_WithThisFilesPath()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(path);
        var seen = new List<string>();
        document.SaveRunner = (file, write) =>
        {
            seen.Add(file);
            write();
        };

        document.AddSequence(new UndoManager());

        seen.ShouldBe(new[] { document.Path.FullPath });
        SequenceListSave.FromFile(path).Sequences.Count.ShouldBe(2);
    }

    private static void WriteSource(string folder, string name, params string[] textures)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        foreach (string texture in textures)
            chain.Frames.Add(new AnimationFrameSave { TextureName = texture, FrameLength = 0.1f });
        list.AnimationChains.Add(chain);
        list.SaveJson(Path.Combine(folder, name));
    }

    [Fact]
    public void WatchedFiles_ListEverySourceAndTheTexturesTheirFramesUse_EvenOutsideTheSeqjFolder()
    {
        using var dir = new TestHelpers.TempDir();
        string art = Path.Combine(dir.Path, "art");
        string sheets = Path.Combine(dir.Path, "sheets");
        Directory.CreateDirectory(art);
        Directory.CreateDirectory(sheets);
        WriteSource(art, "hero.achj", "..\\sheets\\hero.png", "..\\sheets\\hero.png");
        string seqj = Path.Combine(dir.Path, "boss.seqj");
        new SequenceListSave { Sources = { "art/hero.achj", "art/ghost.achj" } }.Save(seqj);

        var document = SequenceDocument.Load(seqj);

        document.WatchedSourceFiles.Select(p => new AnimationEditor.Core.Paths.FilePath(p).FullPath).ShouldBe(new[]
        {
            new AnimationEditor.Core.Paths.FilePath(Path.Combine(art, "hero.achj")).FullPath,
            new AnimationEditor.Core.Paths.FilePath(Path.Combine(art, "ghost.achj")).FullPath,
        });
        document.WatchedTextureFiles.Select(p => new AnimationEditor.Core.Paths.FilePath(p).FullPath).ShouldBe(new[]
        {
            new AnimationEditor.Core.Paths.FilePath(Path.Combine(sheets, "hero.png")).FullPath,
        });
    }

    [Fact]
    public void WatchedFiles_FollowAddedAndRemovedSources()
    {
        using var dir = new TestHelpers.TempDir();
        WriteSource(dir.Path, "hero.achj", "hero.png");
        string seqj = WriteSeqj(dir.Path, "Idle");
        var document = SequenceDocument.Load(seqj);
        var undo = new UndoManager();
        document.WatchedSourceFiles.ShouldBeEmpty();

        document.AddSources(undo, new[] { Path.Combine(dir.Path, "hero.achj") });
        document.WatchedSourceFiles.Count.ShouldBe(1);
        document.WatchedTextureFiles.Count.ShouldBe(1);

        undo.Undo();
        document.WatchedSourceFiles.ShouldBeEmpty();
        document.WatchedTextureFiles.ShouldBeEmpty();
    }
}
