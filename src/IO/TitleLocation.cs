using System;
using System.IO;

namespace FlatRedBall2.IO;

/// <summary>
/// The folder MonoGame's <c>TitleContainer</c> reads content from, for the code that has to read
/// the same files without going through it (directory listing, <c>Song.FromUri</c>).
/// </summary>
/// <remarks>
/// A copy of MonoGame's rule rather than a read of <c>TitleContainer.Location</c>, which is internal
/// and only reachable by reflection (not AOT-safe). Keep it in step with
/// https://github.com/MonoGame/MonoGame/blob/develop/MonoGame.Framework/Platform/TitleContainer.Desktop.cs
/// </remarks>
internal static class TitleLocation
{
    /// <summary>The title location for the running process.</summary>
    internal static string Default { get; } =
        Resolve(AppContext.BaseDirectory, OperatingSystem.IsMacOS(), Directory.Exists);

    /// <summary>
    /// On macOS, <c>../Resources</c> then <c>../../Resources</c> relative to
    /// <paramref name="baseDirectory"/>, whichever exists first; otherwise
    /// <paramref name="baseDirectory"/>. A folder check, not a per-file one: once a <c>.app</c> has a
    /// <c>Contents/Resources</c> folder, every content read goes there.
    /// </summary>
    internal static string Resolve(string baseDirectory, bool isMacOS, Func<string, bool> directoryExists)
    {
        if (isMacOS)
        {
            string sibling = Path.Combine(baseDirectory, "..", "Resources");
            if (directoryExists(sibling))
                return sibling;

            string grandparent = Path.Combine(baseDirectory, "..", "..", "Resources");
            if (directoryExists(grandparent))
                return grandparent;
        }

        return baseDirectory;
    }
}
