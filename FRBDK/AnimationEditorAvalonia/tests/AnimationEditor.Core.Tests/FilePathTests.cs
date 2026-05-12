using AnimationEditor.Core.Paths;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for the locally-owned <see cref="FilePath"/>, lifted from FRB1.
/// Covers the semantics AE relies on: case-insensitive comparison via
/// <see cref="FilePath.Standardized"/>, case preservation via
/// <see cref="FilePath.StandardizedCaseSensitive"/>/<see cref="FilePath.FullPath"/>,
/// forward-slash normalization, and ../ collapsing.
/// </summary>
public class FilePathTests
{
    [Fact]
    public void CompareTo_FullPathOrdering_SortsAlphabetically()
    {
        var a = new FilePath("C:/a.png");
        var b = new FilePath("C:/b.png");

        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
    }

    [Fact]
    public void Equality_CaseInsensitive_TreatsDifferentCasingAsEqual()
    {
        var lower = new FilePath("C:/Games/Hero.achx");
        var upper = new FilePath("C:/GAMES/HERO.ACHX");

        Assert.True(lower == upper);
        Assert.Equal(lower, upper);
        Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
    }

    [Fact]
    public void Equality_StringComparison_TreatsEquivalentStringAsEqual()
    {
        var path = new FilePath("C:/Games/Hero.achx");

        Assert.True(path.Equals("c:/games/hero.achx"));
    }

    [Fact]
    public void Extension_ReturnsLowercaseWithoutPeriod()
    {
        var path = new FilePath("C:/Sprite.PNG");

        Assert.Equal("png", path.Extension);
    }

    [Fact]
    public void FullPath_PreservesOriginalCase()
    {
        var path = new FilePath("C:/Games/Hero.achx");

        Assert.Contains("Hero", path.FullPath);
    }

    [Fact]
    public void FullPath_RelativePath_PrependsCurrentDirectory()
    {
        var path = new FilePath("subdir/file.png");

        // Resolved against Environment.CurrentDirectory; we assert structure, not the absolute prefix.
        Assert.EndsWith("subdir/file.png", path.FullPath);
        Assert.DoesNotContain('\\', path.FullPath);
    }

    [Fact]
    public void FullPath_WithBackslashes_NormalizesToForwardSlashes()
    {
        var path = new FilePath(@"C:\Games\Hero.achx");

        Assert.Equal("C:/Games/Hero.achx", path.FullPath);
    }

    [Fact]
    public void FullPath_WithDotDotSlash_CollapsesParentTraversal()
    {
        var path = new FilePath("C:/Games/Sub/../Hero.achx");

        Assert.Equal("C:/Games/Hero.achx", path.FullPath);
    }

    [Fact]
    public void GetDirectoryContainingThis_ReturnsParentDirectoryWithTrailingSlash()
    {
        var path = new FilePath("C:/Games/Sprites/hero.png");

        var dir = path.GetDirectoryContainingThis();

        Assert.Equal("C:/Games/Sprites/", dir.FullPath);
    }

    [Fact]
    public void ImplicitFromString_NullInput_ReturnsNull()
    {
        FilePath? p = (string?)null;

        Assert.Null(p);
    }

    [Fact]
    public void IsRootOf_ParentDirectory_ReturnsTrue()
    {
        var parent = new FilePath("C:/Games/");
        var child = new FilePath("C:/Games/Hero.achx");

        Assert.True(parent.IsRootOf(child));
        Assert.False(child.IsRootOf(parent));
    }

    [Fact]
    public void RelativeTo_SharedAncestor_ProducesDotDotPrefixedPath()
    {
        var file = new FilePath("C:/Games/A/file.png");
        var anchor = new FilePath("C:/Games/B/");

        var relative = file.RelativeTo(anchor);

        Assert.Equal("../A/file.png", relative);
    }

    [Fact]
    public void Standardized_LowercasesForComparison()
    {
        var path = new FilePath("C:/Games/Hero.achx");

        Assert.Equal("c:/games/hero.achx", path.Standardized);
    }
}
