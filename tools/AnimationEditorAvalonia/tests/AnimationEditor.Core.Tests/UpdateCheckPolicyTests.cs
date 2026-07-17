using AnimationEditor.Core.Update;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="UpdateCheckPolicy"/> — the 24h cache window that keeps the
/// startup update check from hitting GitHub's anonymous rate limit on every launch.
/// </summary>
public class UpdateCheckPolicyTests
{
    [Fact]
    public void ShouldCheck_NoPriorCheck_ReturnsTrue()
    {
        Assert.True(UpdateCheckPolicy.ShouldCheck(null, new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ShouldCheck_WithinCacheWindow_ReturnsFalse()
    {
        var lastCheck = new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc);
        var now = lastCheck.AddHours(1);

        Assert.False(UpdateCheckPolicy.ShouldCheck(lastCheck, now));
    }

    [Fact]
    public void ShouldCheck_ExactlyAtCacheWindow_ReturnsTrue()
    {
        var lastCheck = new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc);
        var now = lastCheck + UpdateCheckPolicy.CacheWindow;

        Assert.True(UpdateCheckPolicy.ShouldCheck(lastCheck, now));
    }

    [Fact]
    public void ShouldCheck_PastCacheWindow_ReturnsTrue()
    {
        var lastCheck = new DateTime(2026, 7, 17, 8, 0, 0, DateTimeKind.Utc);
        var now = lastCheck.AddHours(25);

        Assert.True(UpdateCheckPolicy.ShouldCheck(lastCheck, now));
    }
}
