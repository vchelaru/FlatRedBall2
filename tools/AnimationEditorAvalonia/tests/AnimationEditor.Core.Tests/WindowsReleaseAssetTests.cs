using System.Collections.Generic;
using AnimationEditor.Core.Update;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="WindowsReleaseAsset.FindDownloadUrl"/> — picks the win-x64 zip
/// out of a release's asset list (which also has linux/macOS builds) by exact name match
/// against what the release workflow actually publishes.
/// </summary>
public class WindowsReleaseAssetTests
{
    [Fact]
    public void FindDownloadUrl_WindowsAssetPresent_ReturnsItsUrl()
    {
        var assets = new List<ReleaseAsset>
        {
            new() { Name = "AnimationEditor-linux-x64.tar.gz", BrowserDownloadUrl = "https://example.com/linux" },
            new() { Name = "AnimationEditor-win-x64.zip", BrowserDownloadUrl = "https://example.com/win" },
            new() { Name = "AnimationEditor-osx-x64.zip", BrowserDownloadUrl = "https://example.com/osx" },
        };

        var url = WindowsReleaseAsset.FindDownloadUrl(assets);

        Assert.Equal("https://example.com/win", url);
    }

    [Fact]
    public void FindDownloadUrl_NoWindowsAsset_ReturnsNull()
    {
        var assets = new List<ReleaseAsset>
        {
            new() { Name = "AnimationEditor-linux-x64.tar.gz", BrowserDownloadUrl = "https://example.com/linux" },
        };

        var url = WindowsReleaseAsset.FindDownloadUrl(assets);

        Assert.Null(url);
    }

    [Fact]
    public void FindDownloadUrl_EmptyAssetList_ReturnsNull()
    {
        var url = WindowsReleaseAsset.FindDownloadUrl(new List<ReleaseAsset>());

        Assert.Null(url);
    }
}
