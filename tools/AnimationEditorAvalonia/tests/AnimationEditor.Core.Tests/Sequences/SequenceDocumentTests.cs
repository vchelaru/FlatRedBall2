using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class SequenceDocumentTests
{
    private static string WriteSeqj(string folder, SequenceListSave save)
    {
        string path = Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return path;
    }

    private static void WriteAchj(string folder, string name, string chainName)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(new AnimationChainSave { Name = chainName });
        list.SaveJson(Path.Combine(folder, name));
    }

    [Fact]
    public void Load_ExistingSource_LoadsItsChains()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchj(dir.Path, "hero.achj", "Walk");
        string path = WriteSeqj(dir.Path, new SequenceListSave { Sources = { "hero.achj" } });

        var document = SequenceDocument.Load(path);

        document.IsSourceMissing("hero.achj").ShouldBeFalse();
        document.SourceChains("hero.achj")!.AnimationChains.Single().Name.ShouldBe("Walk");
    }

    [Fact]
    public void Load_MissingSource_IsFlaggedAndKeptInTheFile()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteSeqj(dir.Path, new SequenceListSave { Sources = { "gone.achx" } });
        var document = SequenceDocument.Load(path);

        document.AddSequence(new UndoManager());

        document.IsSourceMissing("gone.achx").ShouldBeTrue();
        document.SourceChains("gone.achx").ShouldBeNull();
        SequenceListSave.FromFile(path).Sources.ShouldBe(new[] { "gone.achx" });
    }

    [Fact]
    public void AddSequence_GivesAUniqueName_SavesAndUndoes()
    {
        using var dir = new TestHelpers.TempDir();
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Sequence" });
        string path = WriteSeqj(dir.Path, save);
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();

        string added = document.AddSequence(undo);

        added.ShouldBe("Sequence 2");
        undo.UndoHistory.Single().Description.ShouldBe("Add Sequence 'Sequence 2'");
        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Sequence", "Sequence 2" });
        undo.Undo();
        document.Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "Sequence" });
        SequenceListSave.FromFile(path).Sequences.Select(s => s.Name).ShouldBe(new[] { "Sequence" });
    }

    [Fact]
    public void RenameSequence_DuplicateName_IsRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        var document = SequenceDocument.Load(WriteSeqj(dir.Path, save));
        var undo = new UndoManager();

        string? error = document.RenameSequence(undo, 1, "Idle");

        error.ShouldNotBeNull();
        undo.CanUndo.ShouldBeFalse();
        document.RenameSequence(undo, 1, "Charge").ShouldBeNull();
        document.Save.Sequences[1].Name.ShouldBe("Charge");
    }

    [Fact]
    public void Save_RoundTripsTracksAndChainReferences()
    {
        using var dir = new TestHelpers.TempDir();
        var save = new SequenceListSave { Sources = { "hero.achj" } };
        save.Sequences.Add(new SequenceSave
        {
            Name = "Attack",
            Loop = true,
            Tracks =
            {
                new SequenceTrackSave
                {
                    Name = "Body",
                    Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Swing", Start = 0.5, Loops = 1.5f } },
                },
            },
        });
        string path = WriteSeqj(dir.Path, save);
        string before = File.ReadAllText(path);
        var document = SequenceDocument.Load(path);

        document.SaveToDisk();

        File.ReadAllText(path).ShouldBe(before);
        document.SaveError.ShouldBeNull();
    }
}
