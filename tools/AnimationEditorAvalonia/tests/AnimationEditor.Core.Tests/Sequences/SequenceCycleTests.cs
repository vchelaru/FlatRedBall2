using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A <c>.seqj</c> edited outside the editor so its references form a cycle (#1793): it loads, and the closing references play nothing.</summary>
public class SequenceCycleTests
{
    // body.achj: Walk and Run, one 0.5 s frame each. A plays Walk and references B; B plays Run and, when cyclic, references A.
    private static string Write(TestHelpers.TempDir dir, bool cyclic)
    {
        var list = new AnimationChainListSave();
        foreach (string chain in new[] { "Walk", "Run" })
        {
            var save = new AnimationChainSave { Name = chain };
            save.Frames.Add(new AnimationFrameSave { TextureName = "body.png", FrameLength = 0.5f, RightCoordinate = 1, BottomCoordinate = 1 });
            list.AnimationChains.Add(save);
        }
        list.SaveJson(Path.Combine(dir.Path, "body.achj"));

        var a = new SequenceSave { Name = "A", Tracks = { ChainTrack("Body", "Walk"), RefTrack("b", "B") } };
        var b = new SequenceSave { Name = "B", Tracks = { ChainTrack("Legs", "Run") } };
        if (cyclic) b.Tracks.Add(RefTrack("a", "A"));
        var file = new SequenceListSave { Sources = { "body.achj" }, Sequences = { a, b } };
        string path = Path.Combine(dir.Path, "boss.seqj");
        file.Save(path);
        return path;
    }

    private static SequenceTrackSave ChainTrack(string name, string chain) =>
        new() { Name = name, Chains = { new SequenceChainReferenceSave { Source = "body.achj", Chain = chain, Start = 0, Loops = 1 } } };

    private static SequenceTrackSave RefTrack(string name, string target) =>
        new() { Name = name, Reference = new SequenceReferenceSave { Sequence = target, Start = 0, Loops = 1 } };

    [Fact]
    public void CyclicFile_Loads_TheClosingReferenceIsBrokenAndNamesTheCycle_AndSavesUnchanged()
    {
        using var dir = new TestHelpers.TempDir();
        string path = Write(dir, cyclic: true);
        string written = File.ReadAllText(path);

        var document = SequenceDocument.Load(path);

        // A shows only its own Walk: the reference to B plays B back into A, so it plays nothing.
        SequenceScene.At(document, 0, 0.1).Select(sprite => sprite.Chain.Name).ShouldBe(new[] { "Walk" });
        var clip = TimelineReferences.For(document.Save.Sequences[0], document.Save.Sequences, _ => 0.5).Single();
        clip.IsBroken.ShouldBeTrue();
        clip.Cycle.ShouldBe(new[] { "A", "B", "A" });
        clip.Length.ShouldBe(TimelineLayout.MissingLength);
        SequenceCycleNotice.For(document.Save, "boss.seqj").ShouldBe(
            "In boss.seqj, A > B > A: these sequences play each other, so those references play nothing until one is removed or retargeted.");

        // An unrelated edit still applies; the reference is written back as it was.
        document.SetLoop(new UndoManager(), 1, false);
        document.Save.Sequences[1].Loop.ShouldBeFalse();
        document.SetLoop(new UndoManager(), 1, true);
        File.ReadAllText(path).ShouldBe(written);
    }

    [Fact]
    public void ExternalEditAddingACycle_Reloads_AndTheNoticeNamesTheCycle()
    {
        using var dir = new TestHelpers.TempDir();
        string path = Write(dir, cyclic: false);
        var document = SequenceDocument.Load(path);
        SequenceCycleNotice.For(document.Save, "boss.seqj").ShouldBeNull();

        Write(dir, cyclic: true);
        var result = document.ReloadFromDisk();

        result.Error.ShouldBeNull();
        result.Changed.ShouldBeTrue();
        document.StaleOnDiskReason.ShouldBeNull();
        SequenceCycleNotice.For(document.Save, "boss.seqj")!.ShouldContain("A > B > A");
    }
}
