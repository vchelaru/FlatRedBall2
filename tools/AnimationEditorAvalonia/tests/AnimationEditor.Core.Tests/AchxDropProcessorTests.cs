using AnimationEditor.Core.DragDrop;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class AchxDropProcessorTests
{
    [Fact]
    public void ContainsAchx_MixedPayload_ReturnsTrue()
    {
        var paths = new[] { @"C:\art\hero.png", @"C:\anims\hero.achx" };

        Assert.True(AchxDropProcessor.ContainsAchx(paths));
    }

    [Fact]
    public void ContainsAchx_NoAchx_ReturnsFalse()
    {
        var paths = new[] { @"C:\art\hero.png", @"C:\art\enemy.jpg" };

        Assert.False(AchxDropProcessor.ContainsAchx(paths));
    }

    [Fact]
    public void SelectAchxFiles_IsCaseInsensitive()
    {
        // OS file managers preserve on-disk casing; an uppercase extension must still match.
        var result = AchxDropProcessor.SelectAchxFiles(new[] { @"C:\anims\Hero.ACHX" });

        Assert.Equal(new[] { @"C:\anims\Hero.ACHX" }, result);
    }

    [Fact]
    public void SelectAchxFiles_MixedPayload_KeepsOnlyAchxInOriginalOrder()
    {
        // Backslash + forward-slash literals prove extension parsing is separator-agnostic.
        var paths = new[]
        {
            @"C:\anims\b.achx",
            @"C:\art\hero.png",
            "/home/user/a.achx",
        };

        var result = AchxDropProcessor.SelectAchxFiles(paths);

        Assert.Equal(new[] { @"C:\anims\b.achx", "/home/user/a.achx" }, result);
    }

    [Fact]
    public void SelectAchxFiles_NullOrEmptyEntries_AreSkipped()
    {
        var paths = new[] { null, "", "   ", @"C:\anims\hero.achx" };

        var result = AchxDropProcessor.SelectAchxFiles(paths);

        Assert.Equal(new[] { @"C:\anims\hero.achx" }, result);
    }
}
