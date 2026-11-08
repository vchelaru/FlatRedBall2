using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Sequences are keyed by name everywhere, so a hand-edited file with two of the same name would hide the second. Loading
/// numbers the later ones in memory only, says so, and leaves the file alone until the user edits.
/// </summary>
public class SequenceDocumentDuplicateNamesTests
{
    private static string WriteDuplicates(string folder)
    {
        string path = Path.Combine(folder, "boss.seqj");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        var second = new SequenceSave { Name = "Idle" };
        second.Tracks.Add(new SequenceTrackSave { Name = "OnlyInSecond" });
        save.Sequences.Add(second);
        save.Sequences.Add(new SequenceSave { Name = "idle" });
        save.Save(path);
        return path;
    }

    [Fact]
    public void Load_NumbersLaterDuplicates_SoEverySequenceIsReachable_AndNamesThem()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteDuplicates(dir.Path);

        var document = SequenceDocument.Load(path);

        document.Save.Sequences.Select(sequence => sequence.Name).ShouldBe(new[] { "Idle", "Idle 2", "idle 3" });
        document.Save.Sequences[1].Tracks.Single().Name.ShouldBe("OnlyInSecond");
        document.DuplicateNamesNotice.ShouldNotBeNull();
        document.DuplicateNamesNotice.ShouldContain("'Idle' as 'Idle 2'");
        document.DuplicateNamesNotice.ShouldContain("'idle' as 'idle 3'");
    }

    [Fact]
    public void Load_DoesNotRewriteTheFile()
    {
        using var dir = new TestHelpers.TempDir();
        string path = WriteDuplicates(dir.Path);
        string before = File.ReadAllText(path);

        SequenceDocument.Load(path);

        File.ReadAllText(path).ShouldBe(before);
    }

    [Fact]
    public void Load_UniqueNames_HasNoNotice()
    {
        using var dir = new TestHelpers.TempDir();
        string path = Path.Combine(dir.Path, "boss.seqj");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(path);

        SequenceDocument.Load(path).DuplicateNamesNotice.ShouldBeNull();
    }
}
