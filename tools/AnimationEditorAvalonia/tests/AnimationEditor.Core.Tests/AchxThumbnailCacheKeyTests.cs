using System;
using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Issue #839: the disk thumbnail cache is invalidated by comparing the source .achx's Size/Modified
// (FolderSnapshotDiff's own invalidation pair) against what's baked into the cache file's name --
// no separate hashing/metadata scheme.
public class AchxThumbnailCacheKeyTests
{
    [Fact]
    public void BuildFileName_SameInputs_ReturnsTheSameFileName()
    {
        var modified = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\hero.achx", 1024, modified);
        var second = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\hero.achx", 1024, modified);

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildFileName_DifferentModified_ReturnsADifferentFileName()
    {
        var path = @"C:\Content\hero.achx";
        var original = AchxThumbnailCacheKey.BuildFileName(path, 1024, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var afterEdit = AchxThumbnailCacheKey.BuildFileName(path, 1024, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.NotEqual(original, afterEdit);
    }

    [Fact]
    public void BuildFileName_DifferentPath_ReturnsADifferentFileName()
    {
        var modified = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var hero = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\hero.achx", 1024, modified);
        var enemy = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\enemy.achx", 1024, modified);

        Assert.NotEqual(hero, enemy);
    }

    [Fact]
    public void BuildFileName_EndsWithPngExtension()
    {
        var result = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\hero.achx", 1024,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.EndsWith(".png", result);
    }
}
