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

/// <summary>Two sequence names that differ only by case.</summary>
public class SequenceNameCaseTests
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

    // Names are matched exactly everywhere (SequenceReferences.Find), so "Walk" and "walk" are two sequences.
    [Fact]
    public void Load_SequencesDifferingOnlyByCase_ReferenceStillPlaysItsTargetAfterAnEdit()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Write(dir.Path, CaseOnlyJson);
        var document = SequenceDocument.Load(path);

        document.SetLoop(new UndoManager(), 0, false);

        var saved = SequenceListSave.FromFile(path);
        var clip = saved.Sequences[2].Tracks[0].References![0];
        var target = SequenceReferences.Find(saved.Sequences, clip.Sequence);
        target.ShouldNotBeNull($"Host plays {clip.Sequence}, but the file now has {string.Join(", ", saved.Sequences.Select(s => s.Name))}");
        target.Tracks[0].Name.ShouldBe("B");
    }

    [Fact]
    public void RenameSequence_ToANameDifferingOnlyByCase_SurvivesAReload()
    {
        using var dir = new TestHelpers.TempDir();
        WriteAchx(Path.Combine(dir.Path, "hero.achx"));
        string path = Write(dir.Path, CaseOnlyJson.Replace("\"walk\"", "\"Stroll\""));
        var document = SequenceDocument.Load(path);

        string? refused = document.RenameSequence(new UndoManager(), 1, "walk");

        if (refused is null)
            SequenceDocument.Load(path).Save.Sequences.Select(s => s.Name).ShouldBe(new[] { "Walk", "walk", "Host" });
    }
}
