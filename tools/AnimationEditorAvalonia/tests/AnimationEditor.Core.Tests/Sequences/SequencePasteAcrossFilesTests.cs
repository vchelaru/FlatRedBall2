using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Copy and paste between two files in different folders, and other green checks of the source list.</summary>
public class SequencePasteAcrossFilesTests
{
    private static string WriteAchx(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < 5; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, RightCoordinate = 16, BottomCoordinate = 16 });
        list.AnimationChains.Add(chain);
        list.Save(path);
        return path;
    }

    private static string Write(string folder, string json)
    {
        string path = Path.Combine(folder, "boss.seqj");
        File.WriteAllText(path, json);
        return path;
    }

    private const string CaseOnlyJson = """
        {
          "version": 1,
          "sources": ["hero.achx"],
          "sequences": [
            { "name": "Walk", "loop": true, "tracks": [ { "name": "A", "chains": [ { "source": "hero.achx", "chain": "Walk", "start": 0 } ] } ] },
            { "name": "walk", "loop": true, "tracks": [ { "name": "B", "chains": [ { "source": "hero.achx", "chain": "Walk", "start": 1 } ] } ] },
            { "name": "Host", "loop": true, "tracks": [ { "name": "R", "references": [ { "sequence": "walk", "start": 0 } ] } ] }
          ]
        }
        """;

    [Fact]
    public void Load_SourceClimbingAboveTheRoot_OpensWithTheSourceMissing()
    {
        using var dir = new TestHelpers.TempDir();
        string up = string.Concat(Enumerable.Repeat("../", 40)) + "hero.achx";
        string path = Write(dir.Path, $$"""{ "version": 1, "sources": ["{{up}}"], "sequences": [ { "name": "A" } ] }""");

        SequenceDocument.Load(path).IsSourceMissing(up).ShouldBeTrue();
    }

    [Fact]
    public void ReplaceSource_ListedTwice_MovesEveryBlock()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string other = WriteAchx(Path.Combine(dir.Path, "other.achx"));
        string path = Write(dir.Path, """
            { "version": 1, "sources": ["hero.achx", "Hero.achx"], "sequences": [ { "name": "A", "tracks": [ { "name": "T", "chains": [
              { "source": "Hero.achx", "chain": "Walk", "start": 0 } ] } ] } ] }
            """);
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        string before = document.Save.ToJsonString();

        string? refused = document.ReplaceSource(undo, "hero.achx", other);

        refused.ShouldBeNull();
        document.Save.Sources.ShouldContain(document.Save.Sequences[0].Tracks[0].Chains[0].Source);
        undo.Undo();
        document.Save.ToJsonString().ShouldBe(before);
    }

    [Fact]
    public void Paste_SeveralSequencesIntoAnotherFolder_KeepsReferencesAndSources_AndUndoRedoAreExact()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        var from = SequenceDocument.Load(Write(dir.Path, CaseOnlyJson.Replace("\"walk\"", "\"Stroll\"")));
        Directory.CreateDirectory(Path.Combine(dir.Path, "sub"));
        string toPath = Write(Path.Combine(dir.Path, "sub"), """{ "version": 1, "sources": [], "sequences": [ { "name": "Host" } ] }""");
        var to = SequenceDocument.Load(toPath);
        SequenceClipboard.TryRead(from.CopySequences(new[] { 0, 1, 2 }), out var content, out _).ShouldBeTrue();
        var undo = new UndoManager();

        var result = to.PasteSequences(undo, content!);

        result.Names.ShouldBe(new[] { "Walk", "Stroll", "Host 2" });
        to.Save.Sources.ShouldBe(new[] { "../hero.achx" });
        var saved = SequenceListSave.FromFile(toPath);
        saved.ToJsonString().ShouldBe(to.Save.ToJsonString());
        saved.Sequences[3].Tracks[0].References![0].Sequence.ShouldBe("Stroll");
        undo.Undo();
        to.Save.Sources.ShouldBeEmpty();
        undo.Redo();
        to.Save.ToJsonString().ShouldBe(saved.ToJsonString());
    }

    // A paste can land on a name an old dangling reference already has.
    [Fact]
    public void PasteSequences_OfACopyWhoseTargetWasDeleted_KeepsTheTargetName()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Write(dir.Path, CaseOnlyJson.Replace("\"walk\"", "\"Stroll\""));
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        SequenceClipboard.TryRead(document.CopySequences(new[] { 1 }), out var content, out _).ShouldBeTrue();
        document.DeleteSequences(undo, new[] { 1 });

        document.PasteSequences(undo, content!);

        document.Save.Sequences.Select(s => s.Name).ShouldContain("Stroll");
        document.Save.Sequences.Single(s => s.Name == "Host").Tracks[0].References![0].Sequence.ShouldBe("Stroll");
    }
}
