using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>The path rewriting Save As shares with undo across a Save As: pure string work on a <see cref="SequenceListSave"/>.</summary>
public class SequenceSourcePathsTests
{
    private static SequenceListSave Save(params string[] sources)
    {
        var save = new SequenceListSave();
        save.Sources.AddRange(sources);
        var track = new SequenceTrackSave { Name = "T" };
        foreach (string source in sources)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = "Walk" });
        var sequence = new SequenceSave { Name = "S" };
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        return save;
    }

    private static string[] ChainSources(SequenceListSave save) =>
        save.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains).Select(c => c.Source).ToArray();

    [Fact]
    public void Rebase_IntoANestedFolder_AddsParentSteps_ToTheListAndEveryReference()
    {
        var save = Save("chars/hero.achx", "enemy.achx");

        var unreachable = SequenceSourcePaths.Rebase(save, new FilePath("/work/game"), new FilePath("/work/game/levels/deep"));

        unreachable.ShouldBeEmpty();
        save.Sources.ShouldBe(new[] { "../../chars/hero.achx", "../../enemy.achx" });
        ChainSources(save).ShouldBe(new[] { "../../chars/hero.achx", "../../enemy.achx" });
    }

    [Fact]
    public void Rebase_IntoAParentFolder_PrefixesTheFolderTheFileCameFrom()
    {
        var save = Save("chars/hero.achx", "../shared/fx.achx");

        SequenceSourcePaths.Rebase(save, new FilePath("/work/game/levels"), new FilePath("/work/game"));

        save.Sources.ShouldBe(new[] { "levels/chars/hero.achx", "shared/fx.achx" });
        ChainSources(save).ShouldBe(new[] { "levels/chars/hero.achx", "shared/fx.achx" });
    }

    [Fact]
    public void Rebase_ToTheSameFolder_ChangesNothing()
    {
        var save = Save("chars/hero.achx");

        SequenceSourcePaths.Rebase(save, new FilePath("/work/game"), new FilePath("/work/game")).ShouldBeEmpty();

        save.Sources.ShouldBe(new[] { "chars/hero.achx" });
    }

    [Fact]
    public void Rebase_LeavesAnAbsoluteSourceAlone_ItResolvesFromAnywhere()
    {
        var save = Save("/assets/hero.achx", "enemy.achx");

        SequenceSourcePaths.Rebase(save, new FilePath("/work/game"), new FilePath("/work/other"));

        save.Sources.ShouldBe(new[] { "/assets/hero.achx", "../game/enemy.achx" });
    }

    [Fact]
    public void Rebase_RewritesAReferenceThatDiffersFromItsSourceOnlyByCase()
    {
        var save = Save("chars/hero.achx");
        save.Sequences[0].Tracks[0].Chains[0].Source = "Chars/Hero.achx";

        SequenceSourcePaths.Rebase(save, new FilePath("/work/game"), new FilePath("/work/game/sub"));

        save.Sources.ShouldBe(new[] { "../chars/hero.achx" });
        ChainSources(save).ShouldBe(new[] { "../chars/hero.achx" });
    }

    [Fact]
    public void Rebase_OnAnotherDrive_ReportsTheSource_AndWritesItAbsolute()
    {
        if (!OperatingSystem.IsWindows()) return;   // a drive letter only exists on Windows
        var save = Save("chars/hero.achx");

        var unreachable = SequenceSourcePaths.Rebase(save, new FilePath(@"C:\work\game"), new FilePath(@"D:\other"));

        unreachable.ShouldBe(new[] { "chars/hero.achx" });
        save.Sources.ShouldBe(new[] { "C:/work/game/chars/hero.achx" });
        ChainSources(save).ShouldBe(new[] { "C:/work/game/chars/hero.achx" });
    }
}
