using AnimationEditor.Core.Paths;
using Shouldly;
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

    // #1987: ".." that reaches the root stays there, as Windows and Unix resolve it.
    [Theory]
    [InlineData("/a/../b.png", "/b.png")]
    [InlineData("/a/../../b.png", "/b.png")]
    [InlineData("/../b.png", "/b.png")]
    [InlineData("C:/../a.png", "C:/a.png")]
    [InlineData(@"C:\a\..\..\b.png", "C:/b.png")]
    [InlineData("C:/a/../b.png", "C:/b.png")]
    public void FullPath_DotDotReachesRoot_KeepsRoot(string path, string expected) =>
        new FilePath(path).FullPath.ShouldBe(expected);

    // Enough ".." to pass the root from any current directory.
    [Fact]
    public void FullPath_RelativeDotDotPastRoot_KeepsRootOfCurrentDirectory()
    {
        string path = "a/" + string.Concat(System.Linq.Enumerable.Repeat("../", 60)) + "b.png";
        string root = System.IO.Path.GetPathRoot(System.Environment.CurrentDirectory)!.Replace('\\', '/');

        new FilePath(path).FullPath.ShouldBe(root + "b.png");
    }

    // #1983: a network path (\\server\share\...) keeps its double-slash root however it is spelled.
    [Theory]
    [InlineData(@"//server/share/Art/a.png", "//server/share/Art/a.png")]
    [InlineData(@"\\server\share\Art\a.png", "//server/share/Art/a.png")]
    [InlineData(@"\\server/share\Art/a.png", "//server/share/Art/a.png")]
    [InlineData(@"//server/share/Art//a.png", "//server/share/Art/a.png")]
    [InlineData(@"\\server\share\Art\.\..\a.png", "//server/share/a.png")]
    [InlineData(@"//server/share/../../../a.png", "//server/share/a.png")]
    [InlineData(@"//server/share/Art/", "//server/share/Art/")]
    [InlineData(@"\\?\C:\Art\a.png", "//?/C:/Art/a.png")]
    [InlineData(@"\\.\C:\Art\a.png", "//./C:/Art/a.png")]
    public void FullPath_NetworkPath_KeepsDoubleSlashRoot(string path, string expected)
    {
        new FilePath(path).FullPath.ShouldBe(expected);
        new FilePath(path).StandardizedCaseSensitive.ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"//server/share/Art/a.png")]
    [InlineData(@"\\server\share\Art\a.png")]
    public void GetDirectoryContainingThis_NetworkPath_KeepsDoubleSlashRoot(string path) =>
        new FilePath(path).GetDirectoryContainingThis().FullPath.ShouldBe("//server/share/Art/");

    // No relative path reaches another share, server or drive: Windows stops ../ at the share.
    [Theory]
    [InlineData(@"\\server\share\Art\a.png", @"\\SERVER\share\Anims\", "../Art/a.png")]
    [InlineData(@"//server/share/Anims/a.png", @"\\server\share\Anims", "a.png")]
    [InlineData(@"\\server\share\a.png", @"C:\Anims\", "//server/share/a.png")]
    [InlineData(@"\\server\share\a.png", "/Anims/", "//server/share/a.png")]
    [InlineData(@"\\server\other\a.png", @"\\server\share\", "//server/other/a.png")]
    [InlineData(@"\\elsewhere\share\a.png", @"\\server\share\", "//elsewhere/share/a.png")]
    [InlineData(@"C:\Art\a.png", @"\\server\share\", "C:/Art/a.png")]
    [InlineData("/Art/a.png", @"\\server\share\", "/Art/a.png")]
    public void RelativeTo_NetworkPath_IsRelativeOnlyWithinTheShare(string path, string folder, string expected) =>
        new FilePath(path).RelativeTo(new FilePath(folder)).ShouldBe(expected);
}
