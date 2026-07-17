using AnimationEditor.Core.Update;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="UpdateChecker"/> against a fake <see cref="IGitHubReleaseClient"/> —
/// no real network traffic. Release CI stamps the assembly version as yyyy.M.d (see
/// build-and-release-animation-editor.yml), so a plain <see cref="Version"/> comparison against
/// the release's publish date is the whole algorithm.
/// </summary>
public class UpdateCheckerTests
{
    private sealed class FakeGitHubReleaseClient : IGitHubReleaseClient
    {
        public GitHubReleaseInfo? Response;
        public Exception? ThrowOnGet;

        public Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnGet is not null)
                throw ThrowOnGet;
            return Task.FromResult(Response);
        }
    }

    [Fact]
    public async Task CheckAsync_LatestReleaseNewerThanCurrent_ReturnsUpdateAvailable()
    {
        var client = new FakeGitHubReleaseClient
        {
            Response = new GitHubReleaseInfo
            {
                PublishedAt = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
                HtmlUrl = "https://github.com/vchelaru/FlatRedBall2/releases/tag/ae-Release_July_17_2026",
            }
        };
        var checker = new UpdateChecker(client);

        var result = await checker.CheckAsync(new Version(2026, 7, 16));

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(2026, 7, 17), result.LatestVersion);
        Assert.Equal("https://github.com/vchelaru/FlatRedBall2/releases/tag/ae-Release_July_17_2026", result.ReleaseUrl);
    }

    [Fact]
    public async Task CheckAsync_LatestReleaseSameAsCurrent_ReturnsNoUpdate()
    {
        var client = new FakeGitHubReleaseClient
        {
            Response = new GitHubReleaseInfo
            {
                PublishedAt = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
                HtmlUrl = "https://github.com/vchelaru/FlatRedBall2/releases/tag/ae-Release_July_16_2026",
            }
        };
        var checker = new UpdateChecker(client);

        var result = await checker.CheckAsync(new Version(2026, 7, 16));

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_LatestReleaseOlderThanCurrent_ReturnsNoUpdate()
    {
        // A locally-built pre-release binary can be newer than the last published release.
        var client = new FakeGitHubReleaseClient
        {
            Response = new GitHubReleaseInfo
            {
                PublishedAt = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero),
                HtmlUrl = "https://github.com/vchelaru/FlatRedBall2/releases/tag/ae-Release_July_10_2026",
            }
        };
        var checker = new UpdateChecker(client);

        var result = await checker.CheckAsync(new Version(2026, 7, 16));

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_DevBuildVersion_SkipsCheckEntirely()
    {
        // Local `dotnet build` without -p:Version defaults to 1.0.0.0 — not date-comparable,
        // so the check must not run at all (never claim a dev build is "out of date").
        var client = new FakeGitHubReleaseClient
        {
            Response = new GitHubReleaseInfo
            {
                PublishedAt = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
                HtmlUrl = "https://github.com/vchelaru/FlatRedBall2/releases",
            }
        };
        var checker = new UpdateChecker(client);

        var result = await checker.CheckAsync(new Version(1, 0, 0, 0));

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_ClientThrows_ReturnsNoUpdateSilently()
    {
        var client = new FakeGitHubReleaseClient { ThrowOnGet = new HttpRequestException("offline") };
        var checker = new UpdateChecker(client);

        var result = await checker.CheckAsync(new Version(2026, 7, 16));

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_ClientReturnsNull_ReturnsNoUpdate()
    {
        var client = new FakeGitHubReleaseClient { Response = null };
        var checker = new UpdateChecker(client);

        var result = await checker.CheckAsync(new Version(2026, 7, 16));

        Assert.False(result.IsUpdateAvailable);
    }
}
