using System;
using System.IO;
using System.Linq;

namespace FlatRedBall2.Glue;

/// <summary>
/// How a Glue project should be read. The file-access delegates exist so the loader never depends on
/// <see cref="System.IO"/> directly — tests substitute an in-memory filesystem, and the browser
/// target substitutes its own reader.
/// </summary>
/// <remarks>
/// These live on an options object rather than on static properties deliberately: settable statics
/// leak between tests in the same assembly, and the engine's architecture rules reserve static state
/// for the service singleton.
/// </remarks>
public sealed class GlueLoadOptions
{
    /// <summary>
    /// Maps a requested path to the path that actually exists, or null if nothing matches. The
    /// default falls back to a case-insensitive match so a project authored on Windows still loads
    /// on a case-sensitive filesystem; returning a path that differs from the request is what
    /// triggers the case-mismatch warning.
    /// </summary>
    public Func<string, string?> ResolveFilePath { get; set; } = DefaultResolveFilePath;

    /// <summary>Reads a path already resolved by <see cref="ResolveFilePath"/>.</summary>
    public Func<string, string> ReadAllText { get; set; } = File.ReadAllText;

    /// <summary>
    /// Throw a <see cref="GlueLoadException"/> on the first error instead of collecting it. Off by
    /// default: while the loader cannot build everything a real project contains, a partial load
    /// with diagnostics is far more useful than a hard stop.
    /// </summary>
    public bool Strict { get; set; }

    private static string? DefaultResolveFilePath(string requestedPath)
    {
        if (File.Exists(requestedPath))
            return requestedPath;

        string? directory = Path.GetDirectoryName(requestedPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        string fileName = Path.GetFileName(requestedPath);
        return Directory.EnumerateFiles(directory)
            .FirstOrDefault(candidate => string.Equals(
                Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase));
    }
}
