using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #1961: which .seqj files a chain rename or delete looks at, what the banner says, and which edits report.
public class BrokenSequenceNoticeTests
{
    private static BrokenSequenceFile Broken(string path, params (string Sequence, string Track, string Chain)[] blocks) =>
        new(path, blocks.Select(b => new SeqjChainMatch(
            new SequenceSave { Name = b.Sequence },
            new SequenceTrackSave { Name = b.Track, Chains = { new SequenceChainReferenceSave { Source = "hero.achx", Chain = b.Chain } } },
            0)).ToList());

    [Fact]
    public void FilesToScan_ProjectFolderOpen_TakesOnlyItsSequenceFiles()
    {
        BrokenSequenceNotice.FilesToScan("/game/hero.achx", ["/game/hero.achx", "/game/a/fight.SEQJ", "/game/map.tsx"])
            .ShouldBe(["/game/a/fight.SEQJ"]);
    }

    [Fact]
    public void FilesToScan_NoProjectFolder_TakesTheAnimationFilesOwnFolderOnly()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var seqj = Path.Combine(folder, "fight.seqj");
            File.WriteAllText(seqj, "");
            File.WriteAllText(Path.Combine(folder, "hero.achx"), "");
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(folder, "sub")).FullName, "deep.seqj"), "");

            BrokenSequenceNotice.FilesToScan(Path.Combine(folder, "hero.achx"), projectFiles: null).ShouldBe([seqj]);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void FilesToScan_FolderThatCannotBeListed_IsEmpty()
    {
        BrokenSequenceNotice.FilesToScan(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hero.achx"), projectFiles: null)
            .ShouldBeEmpty();
    }

    private static RemovedChainNames Removed(string verb, params string[] names) => new(new AnimationChainListSave(), verb, names);

    [Fact]
    public void Message_NoBlocks_IsNull()
    {
        BrokenSequenceNotice.Message(Removed("Renaming", "Walk"), [Broken("/game/fight.seqj")]).ShouldBeNull();
    }

    [Fact]
    public void Message_OneBlock_NamesTheAnimationAndThePlace()
    {
        BrokenSequenceNotice.Message(Removed("Renaming", "Walk"), [Broken("/game/fight.seqj", ("Attack", "Body", "Walk"))])
            .ShouldBe("Renaming 'Walk' broke 1 sequence block: fight.seqj (Attack/Body).");
    }

    [Fact]
    public void Message_FewBlocks_NamesEachPlaceOnce()
    {
        BrokenSequenceNotice.Message(Removed("Deleting", "Walk", "Idle"),
            [Broken("/game/fight.seqj", ("Run", "Body", "Walk"), ("Run", "Body", "Walk")), Broken("/game/intro.seqj", ("Intro", "Legs", "Idle"))])
            .ShouldBe("Deleting 2 animations broke 3 sequence blocks: fight.seqj (Run/Body), intro.seqj (Intro/Legs).");
    }

    [Fact]
    public void Message_ManyBlocksAndFiles_IsAShortCountPerFile()
    {
        var many = Enumerable.Range(0, 7)
            .Select(i => Broken($"/game/f{i}.seqj", ("A", "Body", "Walk"), ("B", "Body", "Walk"))).ToList();

        BrokenSequenceNotice.Message(Removed("Deleting", "Walk"), many)
            .ShouldBe("Deleting 'Walk' broke 14 sequence blocks: "
                + "f0.seqj (2), f1.seqj (2), f2.seqj (2), f3.seqj (2), f4.seqj (2) and 2 more files.");
    }

    [Fact]
    public void ChainNamesRemoved_RenameAndDelete_ReportTheLostNames_UndoAndRedoDoNot()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chains = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
        foreach (var name in new[] { "Walk", "Run", "Jump" }) chains.Add(new AnimationChainSave { Name = name });
        var reported = new List<string>();
        ctx.AppCommands.ChainNamesRemoved += removed => reported.Add($"{removed.Verb} {string.Join(",", removed.Names)}");

        ctx.AppCommands.RenameChain(chains[0], "Stride");
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();
        ctx.AppCommands.DeleteAnimationChains([chains[1], chains[2]]);
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        reported.ShouldBe(["Renaming Walk", "Deleting Run,Jump"]);
    }

    [Fact]
    public void ChainNamesRemoved_MixedDelete_ReportsTheLostNames()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chains = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
        foreach (var name in new[] { "Walk", "Run", "Jump" })
            chains.Add(new AnimationChainSave { Name = name, Frames = { new AnimationFrameSave() } });
        var reported = new List<string>();
        ctx.AppCommands.ChainNamesRemoved += removed => reported.Add($"{removed.Verb} {string.Join(",", removed.Names)}");

        ctx.AppCommands.PasteChainsCut([chains[2]], insertIndex: 0); // a move, so its name stays
        ctx.AppCommands.DeleteSelection([chains[1]], [chains[2].Frames[0]], []);

        reported.ShouldBe(["Deleting Walk"]);
    }
}
