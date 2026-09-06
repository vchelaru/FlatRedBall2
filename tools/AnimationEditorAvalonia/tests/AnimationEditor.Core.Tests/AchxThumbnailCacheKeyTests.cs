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
    public void BuildFileName_EmbedsTheRenderVersion_SoOlderRenderersCacheFilesAreNotServed()
    {
        // Issue #1013 changed how a thumbnail is sampled, but an untouched .achx keeps the same
        // Size/Modified, so every pre-fix cache file would still be a name match and keep being
        // served -- the fix would be invisible until each file happened to change. The render
        // version is part of the name; bump AchxThumbnailCacheKey.RenderVersion (and this literal)
        // whenever the rendering changes.
        var result = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\hero.achx", 1024,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Contains("_v1", result);
    }

    [Fact]
    public void BuildFileName_KeepsTheHashAsTheFirstUnderscoreSegment()
    {
        // ProjectTreeThumbnailService.TrySaveToDisk deletes stale entries for the same source by
        // globbing "{name.Split('_')[0]}_*.png", so anything appended to the name must stay after
        // the first underscore or that cleanup silently stops matching (and a render-version bump
        // would orphan every old file instead of replacing it).
        var result = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\hero.achx", 1024,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var hash = result.Split('_')[0];
        Assert.Equal(16, hash.Length);
        Assert.Matches("^[0-9A-F]{16}$", hash);
    }

    [Fact]
    public void BuildFileName_EndsWithPngExtension()
    {
        var result = AchxThumbnailCacheKey.BuildFileName(@"C:\Content\hero.achx", 1024,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.EndsWith(".png", result);
    }
}
