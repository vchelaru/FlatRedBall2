using AnimationEditor.Core.Update;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="UpdateCheckResult.FromCached"/> — rebuilds a result from the
/// persisted <c>AppSettingsModel</c> fields without a network call, so a cache-hit path
/// (within <see cref="UpdateCheckPolicy.CacheWindow"/>) still reports accurately.
/// </summary>
public class UpdateCheckResultTests
{
    [Fact]
    public void FromCached_NoCachedVersion_ReturnsNoUpdate()
    {
        var result = UpdateCheckResult.FromCached(null, null, new Version(2026, 7, 16));

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public void FromCached_UnparsableCachedVersion_ReturnsNoUpdate()
    {
        var result = UpdateCheckResult.FromCached("not-a-version", "https://example.com", new Version(2026, 7, 16));

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public void FromCached_CachedVersionNewerThanCurrent_ReturnsUpdateAvailable()
    {
        var result = UpdateCheckResult.FromCached("2026.7.17", "https://example.com/latest", new Version(2026, 7, 16));

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(2026, 7, 17), result.LatestVersion);
        Assert.Equal("https://example.com/latest", result.ReleaseUrl);
    }

    [Fact]
    public void FromCached_CachedVersionSameAsCurrent_ReturnsNoUpdate()
    {
        var result = UpdateCheckResult.FromCached("2026.7.16", "https://example.com/latest", new Version(2026, 7, 16));

        Assert.False(result.IsUpdateAvailable);
    }
}
