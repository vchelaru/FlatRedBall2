using AnimationEditor.Core.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class RelativePathSearchFilterTests
{
    private static AchxFileEntry Entry(string relativePath) =>
        new(new FakeEditorFile(relativePath), new FakeEditorFolder("root"), relativePath);

    [Fact]
    public void Filter_EmptyQuery_ReturnsAllEntries()
    {
        var entries = new[] { Entry("hero.achx"), Entry("Sprites/enemy.achx") };

        var result = RelativePathSearchFilter.Filter(entries, e => e.RelativePath, query: "");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_NullQuery_ReturnsAllEntries()
    {
        var entries = new[] { Entry("hero.achx") };

        var result = RelativePathSearchFilter.Filter(entries, e => e.RelativePath, query: null);

        Assert.Single(result);
    }

    [Fact]
    public void Filter_MatchesRelativePathCaseInsensitively()
    {
        var entries = new[] { Entry("Sprites/Hero.achx"), Entry("Sprites/Enemy.achx") };

        var result = RelativePathSearchFilter.Filter(entries, e => e.RelativePath, query: "hero");

        Assert.Equal(["Sprites/Hero.achx"], result.Select(e => e.RelativePath));
    }

    [Fact]
    public void Filter_MatchesOnFolderSegment_NotJustFileName()
    {
        var entries = new[] { Entry("Sprites/hero.achx"), Entry("Enemies/boss.achx") };

        var result = RelativePathSearchFilter.Filter(entries, e => e.RelativePath, query: "sprites");

        Assert.Equal(["Sprites/hero.achx"], result.Select(e => e.RelativePath));
    }

    [Fact]
    public void Filter_NoMatches_ReturnsEmpty()
    {
        var entries = new[] { Entry("hero.achx") };

        var result = RelativePathSearchFilter.Filter(entries, e => e.RelativePath, query: "nonexistent");

        Assert.Empty(result);
    }

    [Fact]
    public void Filter_PngEntries_MatchesRelativePath()
    {
        var entries = new[]
        {
            new PngFileEntry(@"C:\proj\Sprites\hero.png", "Sprites/hero.png"),
            new PngFileEntry(@"C:\proj\Tiles\grass.png", "Tiles/grass.png"),
        };

        var result = RelativePathSearchFilter.Filter(entries, e => e.RelativePath, query: "HERO");

        Assert.Equal(["Sprites/hero.png"], result.Select(e => e.RelativePath));
    }
}
