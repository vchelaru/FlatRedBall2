using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A drag that ends without changing anything, or that something else interrupts.</summary>
public class SequenceGestureInterruptedTests
{
    private static string WriteAchx(string path, int frames = 5, float length = 0.1f)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < frames; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png", FrameLength = length, RightCoordinate = 16, BottomCoordinate = 16 });
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
    public void CancelGesture_NothingMoved_DoesNotWriteTheFile()
    {
        using var dir = new TestHelpers.TempDir();
        string json = "{ \"version\": 1, \"sources\": [], \"sequences\": [ { \"name\": \"A\" }, { \"name\": \"A\" } ] }";
        string path = Write(dir.Path, json);
        var document = SequenceDocument.Load(path);

        document.CancelGesture(document.BeginGesture());

        File.ReadAllText(path).ShouldBe(json);
    }

    [Fact]
    public void CancelGesture_FileRewrittenDuringTheDrag_KeepsTheOutsideChange()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Write(dir.Path, CaseOnlyJson.Replace("\"walk\"", "\"Stroll\""));
        var document = SequenceDocument.Load(path);
        string before = document.BeginGesture();
        document.Save.Sequences[0].Tracks[0].Chains[0].Start = 3;

        File.WriteAllText(path, CaseOnlyJson.Replace("\"walk\"", "\"Stroll\"").Replace("\"Host\"", "\"Outside\""));
        document.ReloadFromDisk();
        document.CancelGesture(before);

        SequenceListSave.FromFile(path).Sequences[2].Name.ShouldBe("Outside");
    }

    private const string TwoSequencesJson = """
        {
          "version": 1,
          "sources": ["hero.achx"],
          "sequences": [
            { "name": "Walk", "loop": true, "tracks": [ { "name": "A", "chains": [ { "source": "hero.achx", "chain": "Walk", "start": 0 } ] } ] },
            { "name": "Host", "loop": true, "tracks": [ { "name": "R", "references": [ { "sequence": "Walk", "start": 0 } ] } ] }
          ]
        }
        """;

    [Fact]
    public void Edit_DuringAGesture_LeavesTheGestureOutOfTheFileAndTheHistory()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Write(dir.Path, TwoSequencesJson);
        var document = SequenceDocument.Load(path);
        var undo = new UndoManager();
        string before = document.BeginGesture();
        document.Save.Sequences[0].Tracks[0].Chains[0].Start = 3;

        document.RenameSequence(undo, 1, "Renamed", keepSelection: true).ShouldBeNull();
        document.CommitGesture(undo, "Move", before);

        SequenceListSave.FromFile(path).Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(0);
        undo.UndoHistory.Select(c => c.Description).ShouldHaveSingleItem();
    }

    [Fact]
    public void SaveToDisk_DuringAGesture_WritesTheStateBeforeIt()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Write(dir.Path, TwoSequencesJson);
        var document = SequenceDocument.Load(path);
        document.BeginGesture();
        document.Save.Sequences[0].Tracks[0].Chains[0].Start = 3;

        document.SaveToDisk();

        SequenceListSave.FromFile(path).Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(0);
    }
}
