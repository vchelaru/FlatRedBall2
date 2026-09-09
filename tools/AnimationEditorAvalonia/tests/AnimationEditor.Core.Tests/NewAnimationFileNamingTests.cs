using AnimationEditor.Core.IO;
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
}
