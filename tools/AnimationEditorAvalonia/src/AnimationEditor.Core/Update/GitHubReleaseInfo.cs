namespace AnimationEditor.Core.Update;

/// <summary>
/// The subset of a GitHub "latest release" API response the update checker needs.
/// </summary>
public sealed class GitHubReleaseInfo
{
    public required DateTimeOffset PublishedAt { get; init; }
    public required string HtmlUrl { get; init; }
}
