using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Update;

/// <summary>
/// Locates the Windows build in a release's asset list. Name matches exactly what
/// build-and-release-animation-editor.yml publishes — see its <c>asset:</c> matrix entries.
/// </summary>
public static class WindowsReleaseAsset
{
    public const string AssetName = "AnimationEditor-win-x64.zip";

    public static string? FindDownloadUrl(IEnumerable<ReleaseAsset> assets) =>
        assets.FirstOrDefault(a => a.Name == AssetName)?.BrowserDownloadUrl;
}
