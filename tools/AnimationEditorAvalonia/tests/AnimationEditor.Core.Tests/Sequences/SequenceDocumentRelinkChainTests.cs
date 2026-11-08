using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Relinking a chain reference to another chain of the same source: one undo entry, also for several chains at once.</summary>
public class SequenceDocumentRelinkChainTests
{
    private static SequenceDocument Open(string folder)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (string chain in new[] { "Walk", "Run", "Jump" }) list.AnimationChains.Add(new AnimationChainSave { Name = chain });
        list.Save(System.IO.Path.Combine(folder, "hero.achx"));
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = "T" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Loops = 1 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Gone", Start = 1, Loops = 1 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Old", Start = 2, Loops = 1 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        string path = System.IO.Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static string[] Labels(UndoManager undo) => undo.UndoHistory.Select(c => c.Description).ToArray();

    private static string[] Chains(SequenceDocument document) =>
        document.Save.Sequences[0].Tracks[0].Chains.Select(c => c.Chain).ToArray();

    [Fact]
    public void RelinkChains_OneMissingChain_PointsAtTheChosenChain_AsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        var undo = new UndoManager();

        document.RelinkChains(undo, 0, new[] { new TimelineChainId(0, 1) }, "Run").ShouldBeNull();

        Chains(document).ShouldBe(new[] { "Walk", "Run", "Old" });
        SequenceListSave.FromFile(document.Path.FullPath).Sequences[0].Tracks[0].Chains[1].Chain.ShouldBe("Run");
        document.LocateDefinition(document.Save.Sequences[0].Tracks[0].Chains[1]).Error.ShouldBeNull();
        Labels(undo).ShouldBe(new[] { "Relink Chain 'Gone' to 'Run'" });

        undo.Undo();
        Chains(document).ShouldBe(new[] { "Walk", "Gone", "Old" });
        undo.Redo();
        Chains(document).ShouldBe(new[] { "Walk", "Run", "Old" });
    }

    [Fact]
    public void RelinkChains_SeveralChains_IsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        var undo = new UndoManager();

        document.RelinkChains(undo, 0, new[] { new TimelineChainId(0, 1), new TimelineChainId(0, 2) }, "Jump").ShouldBeNull();

        Chains(document).ShouldBe(new[] { "Walk", "Jump", "Jump" });
        Labels(undo).ShouldBe(new[] { "Relink 2 Chains to 'Jump'" });
        undo.Undo();
        Chains(document).ShouldBe(new[] { "Walk", "Gone", "Old" });
    }

    [Fact]
    public void RelinkChains_ToAChainTheSourceDoesNotHave_IsRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        var undo = new UndoManager();

        string? error = document.RelinkChains(undo, 0, new[] { new TimelineChainId(0, 1) }, "Fly");

        error.ShouldNotBeNull();
        error.ShouldContain("Fly");
        Chains(document).ShouldBe(new[] { "Walk", "Gone", "Old" });
        Labels(undo).ShouldBeEmpty();
    }

    [Fact]
    public void RelinkChains_ToTheNameItAlreadyHas_ChangesNothing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        var undo = new UndoManager();

        document.RelinkChains(undo, 0, new[] { new TimelineChainId(0, 0) }, "Walk").ShouldBeNull();

        Labels(undo).ShouldBeEmpty();
    }

    [Fact]
    public void RelinkChains_OnAMissingSource_IsRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        document.Save.Sequences[0].Tracks[0].Chains[1].Source = "nothing.achx";
        var undo = new UndoManager();

        document.RelinkChains(undo, 0, new[] { new TimelineChainId(0, 1) }, "Run").ShouldNotBeNull();

        Labels(undo).ShouldBeEmpty();
    }
}
