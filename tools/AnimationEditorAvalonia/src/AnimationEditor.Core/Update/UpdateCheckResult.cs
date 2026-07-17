namespace AnimationEditor.Core.Update;

/// <summary>
/// Result of comparing the running assembly version against the latest published release.
/// </summary>
public sealed record UpdateCheckResult(bool IsUpdateAvailable, Version? LatestVersion, string? ReleaseUrl)
{
    public static readonly UpdateCheckResult NoUpdate = new(false, null, null);

    /// <summary>
    /// Rebuilds a result from the <c>AppSettingsModel</c> cache fields without a network call —
    /// used when <see cref="UpdateCheckPolicy.ShouldCheck"/> says the cache is still fresh.
    /// </summary>
    public static UpdateCheckResult FromCached(string? latestVersionText, string? releaseUrl, Version currentVersion)
    {
        if (string.IsNullOrEmpty(latestVersionText) || !Version.TryParse(latestVersionText, out var latestVersion))
            return NoUpdate;

        return new UpdateCheckResult(latestVersion > currentVersion, latestVersion, releaseUrl);
    }
}
