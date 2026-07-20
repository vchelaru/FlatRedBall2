using AnimationEditor.Core.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AchxSearchFilterTests
{
    private static AchxFileEntry Entry(string relativePath) =>
        new(new FakeEditorFile(relativePath), new FakeEditorFolder("root"), relativePath);

    [Fact]
    public void Filter_EmptyQuery_ReturnsAllEntries()
    {
        var entries = new[] { Entry("hero.achx"), Entry("Sprites/enemy.achx") };

        var result = AchxSearchFilter.Filter(entries, query: "");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Filter_NullQuery_ReturnsAllEntries()
    {
        var entries = new[] { Entry("hero.achx") };

        var result = AchxSearchFilter.Filter(entries, query: null);

        Assert.Single(result);
    }

    [Fact]
    public void Filter_MatchesRelativePathCaseInsensitively()
    {
        var entries = new[] { Entry("Sprites/Hero.achx"), Entry("Sprites/Enemy.achx") };

        var result = AchxSearchFilter.Filter(entries, query: "hero");

        Assert.Equal(["Sprites/Hero.achx"], result.Select(e => e.RelativePath));
    }

    [Fact]
    public void Filter_MatchesOnFolderSegment_NotJustFileName()
    {
        var entries = new[] { Entry("Sprites/hero.achx"), Entry("Enemies/boss.achx") };

        var result = AchxSearchFilter.Filter(entries, query: "sprites");

        Assert.Equal(["Sprites/hero.achx"], result.Select(e => e.RelativePath));
    }

    [Fact]
    public void Filter_NoMatches_ReturnsEmpty()
    {
        var entries = new[] { Entry("hero.achx") };

        var result = AchxSearchFilter.Filter(entries, query: "nonexistent");

        Assert.Empty(result);
    }
}
