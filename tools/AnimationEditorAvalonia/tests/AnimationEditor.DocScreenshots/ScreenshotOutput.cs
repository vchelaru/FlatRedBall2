using System.IO;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Stable on-disk folder for ad-hoc feature-proof screenshots
/// (<c>tools/AnimationEditorAvalonia/tests/_out/&lt;feature&gt;/</c>).
/// </summary>
internal static class ScreenshotOutput
{
    /// <summary>
    /// Resolves <c>tests/_out/&lt;featureName&gt;</c> relative to the DocScreenshots project
    /// (four levels up from the test exe under <c>bin/Debug/netX/</c>).
    /// </summary>
    public static string ResolveFeatureDir(string featureName)
    {
        var testsRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var dir = Path.Combine(testsRoot, "_out", featureName);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
