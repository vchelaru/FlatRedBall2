using AnimationEditor.Core.IO;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>When the folder row's "Convert Animations to .achj" item has something to act on.</summary>
public class ProjectFolderConvertRuleTests
{
    [Fact]
    public void AchxNestedDeepInTheFolder_IsConvertible()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(new[] { "Sprites/Enemies/Bosses/hero.achx" }, "Sprites").ShouldBeTrue();
    }

    [Fact]
    public void OnlyAchjTsxAndSeqj_IsNotConvertible()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(
            new[] { "Sprites/a.achj", "Sprites/b.tsx", "Sprites/c.seqj" }, "Sprites").ShouldBeFalse();
    }

    [Fact]
    public void NoFiles_IsNotConvertible()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(System.Array.Empty<string>(), "Sprites").ShouldBeFalse();
    }

    [Fact]
    public void AchxOutsideTheFolder_IsNotConvertible()
    {
        // "Sprites2" shares a string prefix with "Sprites" but is a different folder.
        ProjectFolderConvertRule.HasConvertibleAchx(
            new[] { "Sprites2/hero.achx", "Other/hero.achx", "hero.achx" }, "Sprites").ShouldBeFalse();
    }

    [Fact]
    public void RootFolder_CountsEveryFile()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(new[] { "Sprites/hero.achx" }, "").ShouldBeTrue();
    }

    [Fact]
    public void ExtensionMatchIgnoresCase()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(new[] { "Sprites/HERO.ACHX" }, "Sprites").ShouldBeTrue();
    }

    [Fact]
    public void AchxOnlyUnderBinOrObjInsideTheFolder_IsNotConvertible()
    {
        // The conversion skips bin/obj below the right-clicked folder, so the item must too.
        ProjectFolderConvertRule.HasConvertibleAchx(
            new[] { "Game/bin/Debug/hero.achx", "Game/obj/hero.achx" }, "Game").ShouldBeFalse();
    }

    [Fact]
    public void RightClickedFolderIsItselfBin_ItsContentsStillCount()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(new[] { "bin/hero.achx" }, "bin").ShouldBeTrue();
    }

    [Fact]
    public void AchxWhoseAchjAlreadyExists_IsNotConvertible()
    {
        // The converter skips a source whose .achj target exists, leaving nothing to do.
        ProjectFolderConvertRule.HasConvertibleAchx(
            new[] { "Sprites/hero.achx", "Sprites/hero.achj" }, "Sprites").ShouldBeFalse();
    }

    [Fact]
    public void OneAchxWithItsAchjAndOneWithout_IsConvertible()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(
            new[] { "Sprites/hero.achx", "Sprites/hero.achj", "Sprites/boss.achx" }, "Sprites").ShouldBeTrue();
    }

    [Fact]
    public void AchjInAnotherFolder_DoesNotBlockTheAchx()
    {
        ProjectFolderConvertRule.HasConvertibleAchx(
            new[] { "Sprites/hero.achx", "Other/hero.achj" }, "Sprites").ShouldBeTrue();
    }
}
