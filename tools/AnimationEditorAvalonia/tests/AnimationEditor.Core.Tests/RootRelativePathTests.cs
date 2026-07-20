using AnimationEditor.Core.Paths;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class RootRelativePathTests
{
    [Fact]
    public void Combine_AbsolutePath_ReturnsNull()
    {
        var result = RootRelativePath.Combine("", @"C:\Outside\box.png");

        Assert.Null(result);
    }

    [Fact]
    public void Combine_AchxInSubfolder_ResolvesTextureRelativeToAchxFolder()
    {
        var result = RootRelativePath.Combine("Chains", "box.png");

        Assert.Equal("Chains/box.png", result);
    }

    [Fact]
    public void Combine_DotDotEscapesRoot_ReturnsNull()
    {
        var result = RootRelativePath.Combine("", "../box.png");

        Assert.Null(result);
    }

    [Fact]
    public void Combine_DotDotWithinRoot_ResolvesUpOneLevel()
    {
        var result = RootRelativePath.Combine("Chains/Player", "../Shared/box.png");

        Assert.Equal("Chains/Shared/box.png", result);
    }

    [Fact]
    public void Combine_SameFolder_ReturnsTextureNameUnchanged()
    {
        var result = RootRelativePath.Combine("", "box.png");

        Assert.Equal("box.png", result);
    }

    [Fact]
    public void Combine_TextureInSubfolder_ReturnsRootRelativePath()
    {
        var result = RootRelativePath.Combine("", "Textures/box.png");

        Assert.Equal("Textures/box.png", result);
    }

    [Fact]
    public void DirectoryOf_NestedFile_ReturnsDirectoryPortion()
    {
        var result = RootRelativePath.DirectoryOf("Chains/player.achx");

        Assert.Equal("Chains", result);
    }

    [Fact]
    public void DirectoryOf_RootLevelFile_ReturnsEmptyString()
    {
        var result = RootRelativePath.DirectoryOf("player.achx");

        Assert.Equal("", result);
    }
}
