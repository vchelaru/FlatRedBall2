namespace AnimationEditor.Core.Update;

/// <summary>One downloadable file attached to a GitHub release (e.g. a platform zip).</summary>
public sealed class ReleaseAsset
{
    public required string Name { get; init; }
    public required string BrowserDownloadUrl { get; init; }
}
