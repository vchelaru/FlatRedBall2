namespace AnimationEditor.Core.Update;

/// <summary>
/// Result of comparing the running assembly version against the latest published release.
/// <see cref="WindowsDownloadUrl"/> is the win-x64 zip asset — present only when a Windows
/// build was found on the release — and is what the auto-updater downloads; <see cref="ReleaseUrl"/>
/// is the human-facing release page, used as the fallback link on other platforms.
/// </summary>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable, Version? LatestVersion, string? ReleaseUrl, string? WindowsDownloadUrl = null)
{
    public static readonly UpdateCheckResult NoUpdate = new(false, null, null);

    /// <summary>
    /// Rebuilds a result from the <c>AppSettingsModel</c> cache fields without a network call —
    /// used when <see cref="UpdateCheckPolicy.ShouldCheck"/> says the cache is still fresh.
    /// </summary>
    public static UpdateCheckResult FromCached(
        string? latestVersionText, string? releaseUrl, string? windowsDownloadUrl, Version currentVersion)
    {
        if (string.IsNullOrEmpty(latestVersionText) || !Version.TryParse(latestVersionText, out var latestVersion))
            return NoUpdate;

        return new UpdateCheckResult(latestVersion > currentVersion, latestVersion, releaseUrl, windowsDownloadUrl);
    }
}
