namespace AnimationEditor.Core.Update;

/// <summary>
/// Compares the running assembly version against the latest published GitHub release.
/// Release CI stamps the assembly version as <c>yyyy.M.d</c> (see
/// build-and-release-animation-editor.yml's <c>-p:Version</c>), so a plain <see cref="Version"/>
/// comparison against the release's publish date is the whole algorithm — no tag-name parsing
/// needed. A local <c>dotnet build</c> without that MSBuild property defaults to 1.0.0.0, which
/// is not comparable, so those builds skip the check entirely rather than risk a false positive.
/// </summary>
public sealed class UpdateChecker : IUpdateChecker
{
    private const int MinReleaseYear = 2000;

    private readonly IGitHubReleaseClient _client;

    public UpdateChecker(IGitHubReleaseClient client) => _client = client;

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        if (currentVersion.Major < MinReleaseYear)
            return UpdateCheckResult.NoUpdate;

        GitHubReleaseInfo? release;
        try
        {
            release = await _client.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Offline / rate-limited / GitHub hiccup — fail silently, never block startup.
            return UpdateCheckResult.NoUpdate;
        }

        if (release is null)
            return UpdateCheckResult.NoUpdate;

        var latestVersion = new Version(release.PublishedAt.Year, release.PublishedAt.Month, release.PublishedAt.Day);
        return new UpdateCheckResult(latestVersion > currentVersion, latestVersion, release.HtmlUrl);
    }
}
