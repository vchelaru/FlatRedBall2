using AnimationEditor.Core.HotReload;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ReferencedFileDiffTests
{
    // 1. Both lists empty → no added, no removed.
    [Fact]
    public void BothEmpty_NoAddedNoRemoved()
    {
        var (added, removed) = ReferencedFileDiff.Diff([], []);
        Assert.Empty(added);
        Assert.Empty(removed);
    }

    // 2. New path added → appears in Added.
    [Fact]
    public void NewPath_AppearsInAdded()
    {
        var (added, removed) = ReferencedFileDiff.Diff(
            ["a.png"],
            ["a.png", "b.png"]);
        Assert.Contains("b.png", added);
        Assert.Empty(removed);
    }

    // 3. Path removed → appears in Removed.
    [Fact]
    public void RemovedPath_AppearsInRemoved()
    {
        var (added, removed) = ReferencedFileDiff.Diff(
            ["a.png", "b.png"],
            ["a.png"]);
        Assert.Empty(added);
        Assert.Contains("b.png", removed);
    }

    // 4. Path in both → neither Added nor Removed.
    [Fact]
    public void SharedPath_NotInAddedOrRemoved()
    {
        var (added, removed) = ReferencedFileDiff.Diff(
            ["a.png"],
            ["a.png"]);
        Assert.Empty(added);
        Assert.Empty(removed);
    }

    // 5. Exact-match case sensitivity (same case passes through correctly).
    [Fact]
    public void ExactMatch_SameCasePath()
    {
        var (added, removed) = ReferencedFileDiff.Diff(
            ["C:/sprites/Hero.png"],
            ["C:/sprites/Hero.png"]);
        Assert.Empty(added);
        Assert.Empty(removed);
    }
}
