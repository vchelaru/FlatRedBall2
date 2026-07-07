using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class FilesPanelScopeFilterTests
{
    // Backslash literals throughout to prove cross-platform path handling (FilePath, not
    // System.IO.Path): these tests must pass on Linux CI against Windows-authored paths.
    private const string AchxFolder = @"C:\proj\Content\Anim\";

    private static PngFileEntry File(string absolute, string relative) => new(absolute, relative);

    // ── Degenerate inputs ─────────────────────────────────────────────────

    [Fact]
    public void NoReferencedTextures_ReturnsEmpty()
    {
        var all = new[] { File(@"C:\proj\Content\Anim\hero.png", "hero.png") };
        var result = FilesPanelScopeFilter.FilterToReferenced(all, new string[0], AchxFolder);
        Assert.Empty(result);
    }

    [Fact]
    public void NoFiles_ReturnsEmpty()
    {
        var result = FilesPanelScopeFilter.FilterToReferenced(
            new PngFileEntry[0], new[] { "hero.png" }, AchxFolder);
        Assert.Empty(result);
    }

    // ── Matching ──────────────────────────────────────────────────────────

    [Fact]
    public void ReferencedFileInAchxFolder_Matched()
    {
        var all = new[]
        {
            File(@"C:\proj\Content\Anim\hero.png", "hero.png"),
            File(@"C:\proj\Content\Anim\unused.png", "unused.png"),
        };
        var result = FilesPanelScopeFilter.FilterToReferenced(all, new[] { "hero.png" }, AchxFolder);
        Assert.Single(result);
        Assert.Equal(@"C:\proj\Content\Anim\hero.png", result[0].AbsolutePath);
    }

    [Fact]
    public void UnreferencedFiles_Excluded()
    {
        var all = new[]
        {
            File(@"C:\proj\Content\Anim\a.png", "a.png"),
            File(@"C:\proj\Content\Anim\b.png", "b.png"),
            File(@"C:\proj\Content\Anim\c.png", "c.png"),
        };
        var result = FilesPanelScopeFilter.FilterToReferenced(all, new[] { "b.png" }, AchxFolder);
        Assert.Single(result);
        Assert.Equal("b.png", result[0].RelativePath);
    }

    [Fact]
    public void MatchIsCaseInsensitive()
    {
        var all = new[] { File(@"C:\proj\Content\Anim\Hero.PNG", "Hero.PNG") };
        var result = FilesPanelScopeFilter.FilterToReferenced(all, new[] { "hero.png" }, AchxFolder);
        Assert.Single(result);
    }

    [Fact]
    public void BackslashInStoredName_Matched()
    {
        var all = new[] { File(@"C:\proj\Content\Anim\Player\walk.png", "Player/walk.png") };
        var result = FilesPanelScopeFilter.FilterToReferenced(all, new[] { @"Player\walk.png" }, AchxFolder);
        Assert.Single(result);
    }

    // ── Root differs from .achx folder (the reason matching is by absolute path) ──

    [Fact]
    public void ReferencedTextureAboveAchxFolder_MatchedByAbsolutePath()
    {
        // Files-panel root is the Content folder (an ancestor of the .achx's Anim folder),
        // so the scan lists a file whose path sits above the .achx. The texture name is
        // stored relative to the .achx folder ("../hero.png") — matching must resolve both
        // to the same absolute path.
        var all = new[]
        {
            File(@"C:\proj\Content\hero.png", "hero.png"),
            File(@"C:\proj\Content\Anim\other.png", "Anim/other.png"),
        };
        var result = FilesPanelScopeFilter.FilterToReferenced(all, new[] { "../hero.png" }, AchxFolder);
        Assert.Single(result);
        Assert.Equal(@"C:\proj\Content\hero.png", result[0].AbsolutePath);
    }

    // ── Referenced texture missing from disk ──────────────────────────────

    [Fact]
    public void ReferencedTextureNotOnDisk_SilentlyOmitted()
    {
        var all = new[] { File(@"C:\proj\Content\Anim\present.png", "present.png") };
        var result = FilesPanelScopeFilter.FilterToReferenced(
            all, new[] { "present.png", "missing.png" }, AchxFolder);
        Assert.Single(result);
        Assert.Equal("present.png", result[0].RelativePath);
    }

    // ── Order preserved ───────────────────────────────────────────────────

    [Fact]
    public void ResultPreservesInputOrder()
    {
        var all = new[]
        {
            File(@"C:\proj\Content\Anim\z.png", "z.png"),
            File(@"C:\proj\Content\Anim\a.png", "a.png"),
        };
        var result = FilesPanelScopeFilter.FilterToReferenced(
            all, new[] { "a.png", "z.png" }, AchxFolder);
        Assert.Equal(new[] { "z.png", "a.png" }, result.Select(f => f.RelativePath).ToArray());
    }
}
