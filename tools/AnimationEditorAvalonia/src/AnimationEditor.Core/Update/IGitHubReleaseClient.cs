namespace AnimationEditor.Core.Update;

/// <summary>
/// Fetches the latest non-draft, non-prerelease GitHub release. Abstracted from
/// <see cref="UpdateChecker"/> so tests can fake it without any real network traffic.
/// </summary>
public interface IGitHubReleaseClient
{
    Task<GitHubReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
}
