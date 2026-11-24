using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #1949: which blocks of a .seqj play given chains of one animation file.
public class SeqjChainUsageScannerTests
{
    private const string SeqjPath = @"C:\game\Content\Sequences\fight.seqj";
    private const string HeroPath = @"C:\game\Content\hero.achx";

    private static SequenceListSave Document(string[] sources, params (string Source, string Chain)[] blocks)
    {
        var track = new SequenceTrackSave { Name = "Body" };
        foreach (var (source, chain) in blocks)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain });
        var document = new SequenceListSave();
        document.Sources.AddRange(sources);
        document.Sequences.Add(new SequenceSave { Name = "Run", Tracks = { track } });
        return document;
    }

    [Fact]
    public void FindMatches_RelativeSources_MatchesOnlyTheNamedChainsOfThatFile()
    {
        var document = Document(["../hero.achx", "hero.achx"],
            ("../hero.achx", "Walk"), ("hero.achx", "Walk"), ("../hero.achx", "Run"), ("../hero.achx", "Jump"));

        var matches = SeqjChainUsageScanner.FindMatches(document, SeqjPath, HeroPath, ["Walk", "Jump"]);

        matches.Select(m => (m.Address, m.Chain)).ShouldBe([("Run/Body/0", "Walk"), ("Run/Body/3", "Jump")]);
    }

    [Fact]
    public void FindMatches_SourceNotInLibrary_ReturnsNothing()
    {
        var document = Document([], ("../hero.achx", "Walk"));

        SeqjChainUsageScanner.FindMatches(document, SeqjPath, HeroPath, ["Walk"]).ShouldBeEmpty();
    }

    [Fact]
    public void FindMatches_SourceDiffersByCase_FollowsTheFileSystem()
    {
        var document = Document(["../HERO.achx"], ("../HERO.achx", "Walk"));

        var matches = SeqjChainUsageScanner.FindMatches(document, SeqjPath, HeroPath, ["Walk"]);

        matches.Count.ShouldBe(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? 1 : 0);
    }

    [Fact]
    public void FindMatches_SourceThatCannotBeResolved_DoesNotHideOtherBlocks()
    {
        // A drive-relative folder has no root for ".." to stop at.
        const string seqjPath = @"C:Sequences\fight.seqj";
        const string tooManyUps = "../../hero.achx";
        var document = Document([tooManyUps, HeroPath], (tooManyUps, "Walk"), (HeroPath, "Walk"));

        var matches = SeqjChainUsageScanner.FindMatches(document, seqjPath, HeroPath, ["Walk"]);

        matches.Select(m => m.Address).ShouldBe(["Run/Body/1"]);
    }

    [Fact]
    public void FindMatches_UnreadableFile_ReturnsNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.seqj");
        File.WriteAllText(path, "{ not json");
        try
        {
            SeqjChainUsageScanner.FindMatches(path, HeroPath, ["Walk"]).ShouldBeEmpty();
            SeqjChainUsageScanner.FindMatches(path + ".missing", HeroPath, ["Walk"]).ShouldBeEmpty();
        }
        finally { File.Delete(path); }
    }
}
