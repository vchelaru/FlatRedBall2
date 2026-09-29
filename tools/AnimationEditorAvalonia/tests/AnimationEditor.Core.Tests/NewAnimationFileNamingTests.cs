using AnimationEditor.Core.IO;
using Shouldly;
using System;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class NewAnimationFileNamingTests
{
    [Fact]
    public void Resolve_NameTakenByOtherExtension_ReturnsError()
    {
        // A .achj project that already has Player.achx: the stem is taken regardless of which
        // extension the new file would get, so this must be rejected rather than suffixed (#1018).
        var result = NewAnimationFileNaming.Resolve("Player", new[] { "Player.achx" }, "achj");

        Assert.Null(result.FileName);
        Assert.Equal("\"Player\" already exists in this folder.", result.Error);
    }

    [Fact]
    public void Resolve_NameWithAnimationExtensionTyped_HonorsTypedExtension()
    {
        var result = NewAnimationFileNaming.Resolve("Player.achx", Array.Empty<string>(), "achj");

        Assert.Equal("Player.achx", result.FileName);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Resolve_ValidName_AppendsProjectExtension()
    {
        var result = NewAnimationFileNaming.Resolve("Player", new[] { "Enemy.achj" }, "achj");

        Assert.Equal("Player.achj", result.FileName);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ResolveExtension_AchxOutnumbersAchj_ReturnsAchx()
    {
        var extension = NewAnimationFileNaming.ResolveExtension(
            new[] { "Player.achx", "Enemy.achx", "Boss.achj" });

        Assert.Equal("achx", extension);
    }

    [Fact]
    public void ResolveExtension_EqualCounts_ReturnsAchj()
    {
        // .achj is the tiebreaker, so a tie (and an empty project) lands on JSON.
        var extension = NewAnimationFileNaming.ResolveExtension(
            new[] { "Player.achx", "Enemy.achj" });

        Assert.Equal("achj", extension);
    }

    [Fact]
    public void SuggestFileName_DefaultNameTaken_UsesNextFreeNumber()
    {
        var suggested = NewAnimationFileNaming.SuggestFileName(
            new[] { "NewAnimation.achj", "NewAnimation2.achx" }, "achj");

        Assert.Equal("NewAnimation3.achj", suggested);
    }

    [Fact]
    public void SuggestDuplicateFileName_NoCollision_AppendsTwoAndKeepsExtension()
    {
        var suggested = NewAnimationFileNaming.SuggestDuplicateFileName("Hero.achx", new[] { "Hero.achx" });

        suggested.ShouldBe("Hero2.achx");
    }

    [Fact]
    public void SuggestDuplicateFileName_SourceEndsInNumber_IncrementsInsteadOfAppending()
    {
        var suggested = NewAnimationFileNaming.SuggestDuplicateFileName(
            "Hero2.achx", new[] { "Hero.achx", "Hero2.achx" });

        suggested.ShouldBe("Hero3.achx");
    }

    [Fact]
    public void SuggestDuplicateFileName_StemTakenByEitherExtension_SkipsToNextFreeNumber()
    {
        // Same rule as duplicated chains ("Walk2", "Walk3") and as the stem-based collision
        // check for new files: hero2.achj blocks Hero2.achx too.
        var suggested = NewAnimationFileNaming.SuggestDuplicateFileName(
            "Hero.achx", new[] { "Hero.achx", "hero2.achj", "Hero3.achx" });

        suggested.ShouldBe("Hero4.achx");
    }
}
