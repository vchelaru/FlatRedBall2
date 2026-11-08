using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// Replace Source swaps one library source for another file: the source list entry changes in place and every chain
/// reference to it follows, as one undo entry. Chains the new file lacks stay on the timeline, flagged missing.
/// </summary>
public class SequenceDocumentReplaceSourceTests
{
    private static string Dir(string folder, params string[] names) => System.IO.Path.Combine(names.Prepend(folder).ToArray());

    private static void WriteAchx(string path, params string[] chains)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (string chain in chains) list.AnimationChains.Add(new AnimationChainSave { Name = chain });
        list.Save(path);
    }

    // hero.achx is the source in the middle of three; "Walk" and "Run" of it are used on two sequences and two tracks.
    private static SequenceDocument Open(string folder, bool heroExists = true)
    {
        if (heroExists) WriteAchx(Dir(folder, "hero.achx"), "Walk", "Run");
        WriteAchx(Dir(folder, "a.achx"), "A1");
        WriteAchx(Dir(folder, "z.achx"), "Z1");
        var save = new SequenceListSave();
        save.Sources.AddRange(new[] { "a.achx", "hero.achx", "z.achx" });
        var first = new SequenceSave { Name = "Attack" };
        var track1 = new SequenceTrackSave { Name = "T1" };
        track1.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Loops = 1 });
        track1.Chains.Add(new SequenceChainReferenceSave { Source = "a.achx", Chain = "A1", Start = 1, Loops = 1 });
        var track2 = new SequenceTrackSave { Name = "T2" };
        track2.Chains.Add(new SequenceChainReferenceSave { Source = "Hero.ACHX", Chain = "Run", Loops = 1 });
        first.Tracks.Add(track1);
        first.Tracks.Add(track2);
        var second = new SequenceSave { Name = "Idle" };
        var track3 = new SequenceTrackSave { Name = "T3" };
        track3.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Loops = 2 });
        second.Tracks.Add(track3);
        save.Sequences.Add(first);
        save.Sequences.Add(second);
        string path = Dir(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    private static string[] Labels(UndoManager undo) => undo.UndoHistory.Select(c => c.Description).ToArray();

    private static string[] ChainSources(SequenceListSave save) =>
        save.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains).Select(c => c.Source).ToArray();

    [Fact]
    public void ReplaceSource_SwapsTheListEntryInPlace_AndEveryReferenceInEverySequenceAndTrack_AsOneUndoEntry()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        WriteAchx(Dir(dir.Path, "heroes.achx"), "Walk", "Run");
        var undo = new UndoManager();

        document.ReplaceSource(undo, "hero.achx", Dir(dir.Path, "heroes.achx")).ShouldBeNull();

        document.Save.Sources.ShouldBe(new[] { "a.achx", "heroes.achx", "z.achx" });
        ChainSources(document.Save).ShouldBe(new[] { "heroes.achx", "a.achx", "heroes.achx", "heroes.achx" });
        SequenceListSave.FromFile(document.Path.FullPath).Sources.ShouldBe(new[] { "a.achx", "heroes.achx", "z.achx" });
        ChainSources(SequenceListSave.FromFile(document.Path.FullPath)).ShouldBe(new[] { "heroes.achx", "a.achx", "heroes.achx", "heroes.achx" });
        Labels(undo).ShouldBe(new[] { "Replace Source 'hero.achx' with 'heroes.achx'" });
        document.WatchedSourceFiles.ShouldContain(new AnimationEditor.Core.Paths.FilePath(Dir(dir.Path, "heroes.achx")).FullPath);
        document.WatchedSourceFiles.ShouldNotContain(new AnimationEditor.Core.Paths.FilePath(Dir(dir.Path, "hero.achx")).FullPath);
    }

    [Fact]
    public void ReplaceSource_Undo_PutsBackTheSourceAndEveryReference_RedoReplacesAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        WriteAchx(Dir(dir.Path, "heroes.achx"), "Walk", "Run");
        var undo = new UndoManager();
        document.ReplaceSource(undo, "hero.achx", Dir(dir.Path, "heroes.achx"));

        undo.Undo();

        document.Save.Sources.ShouldBe(new[] { "a.achx", "hero.achx", "z.achx" });
        ChainSources(document.Save).ShouldBe(new[] { "hero.achx", "a.achx", "Hero.ACHX", "hero.achx" });
        SequenceListSave.FromFile(document.Path.FullPath).Sources.ShouldBe(new[] { "a.achx", "hero.achx", "z.achx" });

        undo.Redo();

        document.Save.Sources.ShouldBe(new[] { "a.achx", "heroes.achx", "z.achx" });
        ChainSources(document.Save).ShouldBe(new[] { "heroes.achx", "a.achx", "heroes.achx", "heroes.achx" });
    }

    [Fact]
    public void ReplaceSource_ChainsTheNewFileLacks_StayOnTheTimeline_FlaggedMissing()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        WriteAchx(Dir(dir.Path, "heroes.achx"), "Walk");   // no "Run"

        document.ReplaceSource(new UndoManager(), "hero.achx", Dir(dir.Path, "heroes.achx")).ShouldBeNull();

        var walk = document.Save.Sequences[0].Tracks[0].Chains[0];
        var run = document.Save.Sequences[0].Tracks[1].Chains[0];
        run.Source.ShouldBe("heroes.achx");
        run.Chain.ShouldBe("Run");
        document.LocateDefinition(walk).Error.ShouldBeNull();
        document.LocateDefinition(run).Error.ShouldNotBeNull();
        document.FindChain(run).ShouldBeNull();
    }

    [Fact]
    public void ReplaceSource_OnAMissingSource_LoadsTheReplacement()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path, heroExists: false);
        document.IsSourceMissing("hero.achx").ShouldBeTrue();
        WriteAchx(Dir(dir.Path, "moved", "hero.achx"), "Walk", "Run");

        document.ReplaceSource(new UndoManager(), "hero.achx", Dir(dir.Path, "moved/hero.achx")).ShouldBeNull();

        document.Save.Sources.ShouldBe(new[] { "a.achx", "moved/hero.achx", "z.achx" });
        document.IsSourceMissing("moved/hero.achx").ShouldBeFalse();
        document.FindChain(document.Save.Sequences[0].Tracks[0].Chains[0]).ShouldNotBeNull();
    }

    [Fact]
    public void ReplaceSource_KeepsTheSelectedSequence()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        document.SelectedSequence = 1;
        WriteAchx(Dir(dir.Path, "heroes.achx"), "Walk", "Run");

        document.ReplaceSource(new UndoManager(), "hero.achx", Dir(dir.Path, "heroes.achx"));

        document.SelectedSequence.ShouldBe(1);
    }

    [Fact]
    public void ReplaceSource_WithAFileAlreadyInTheLibrary_IsRefused_NothingChanges()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        var undo = new UndoManager();

        // "z.achx" spelled another way is still the same file.
        string? error = document.ReplaceSource(undo, "hero.achx", Dir(dir.Path, "sub/../z.achx"));

        error.ShouldNotBeNull();
        error.ShouldContain("z.achx");
        error.ShouldContain("already in the library");
        document.Save.Sources.ShouldBe(new[] { "a.achx", "hero.achx", "z.achx" });
        Labels(undo).ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceSource_WithAFileThatIsNotAnAchxOrAchj_IsRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        File.WriteAllText(Dir(dir.Path, "notes.txt"), "x");
        var undo = new UndoManager();

        string? error = document.ReplaceSource(undo, "hero.achx", Dir(dir.Path, "notes.txt"));

        error.ShouldNotBeNull();
        error.ShouldContain("notes.txt");
        Labels(undo).ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceSource_OfASourceNotInTheLibrary_IsRefused()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        WriteAchx(Dir(dir.Path, "heroes.achx"), "Walk");
        var undo = new UndoManager();

        document.ReplaceSource(undo, "nothing.achx", Dir(dir.Path, "heroes.achx")).ShouldNotBeNull();

        Labels(undo).ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceSource_WithTheSameFile_IsANoOp()
    {
        using var dir = new TestHelpers.TempDir();
        var document = Open(dir.Path);
        var undo = new UndoManager();

        document.ReplaceSource(undo, "hero.achx", Dir(dir.Path, "hero.achx")).ShouldBeNull();

        Labels(undo).ShouldBeEmpty();
        document.Save.Sources.ShouldBe(new[] { "a.achx", "hero.achx", "z.achx" });
    }
}
