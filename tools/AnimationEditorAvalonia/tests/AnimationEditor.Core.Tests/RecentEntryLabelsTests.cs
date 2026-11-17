using AnimationEditor.Core.Paths;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class RecentEntryLabelsTests
{
    [Fact]
    public void Build_UniqueNames_AreUnchanged()
    {
        var labels = RecentEntryLabels.Build(new[] { "C:/A/hero.achx", "C:/B/enemy.achx" });

        Assert.Equal(new[] { "hero.achx", "enemy.achx" }, labels);
    }

    [Fact]
    public void Build_TwoClashingNames_GetNearestParent_AndOthersStayPlain()
    {
        var labels = RecentEntryLabels.Build(new[]
        {
            "C:/Games/CrankyChibiCthulu/Content",
            "C:/Games/Other",
            "C:/Games/KidDefense/Content",
        });

        Assert.Equal(new[] { "Content - CrankyChibiCthulu", "Other", "Content - KidDefense" }, labels);
    }

    [Fact]
    public void Build_ThreeClashingWithDifferingDepth_UsesFewestSegmentsThatMakeAllUnique()
    {
        var labels = RecentEntryLabels.Build(new[]
        {
            "C:/Work/A/Content",
            "C:/Home/A/Content",
            "C:/B/Content",
        });

        Assert.Equal(new[] { "Content - Work/A", "Content - Home/A", "Content - B" }, labels);
    }

    [Fact]
    public void Build_SameNameDifferentDriveRoots_UsesTheRoot()
    {
        var labels = RecentEntryLabels.Build(new[] { "C:/Content", "D:/Content" });

        Assert.Equal(new[] { "Content - C:", "Content - D:" }, labels);
    }

    [Fact]
    public void Build_BackslashesAndCase_AreNormalizedForClashes()
    {
        var labels = RecentEntryLabels.Build(new[] { @"C:\Games\A\content", "C:/Games/B/Content" });

        Assert.Equal(new[] { "content - A", "Content - B" }, labels);
    }

    [Fact]
    public void Build_TrailingSlash_IsIgnored()
    {
        var labels = RecentEntryLabels.Build(new[] { "C:/A/Content/", "C:/B/Content/" });

        Assert.Equal(new[] { "Content - A", "Content - B" }, labels);
    }
}
