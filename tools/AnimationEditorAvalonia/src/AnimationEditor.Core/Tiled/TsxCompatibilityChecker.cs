using DotTiled;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Checks whether a Tiled <see cref="Tileset"/> can be opened as a native AnimationEditor project (issue #1140).
/// </summary>
public static partial class TsxCompatibilityChecker
{
    /// <summary><see cref="TsxWriter"/> writes UTF-8 and keeps the original XML declaration, so a file in any
    /// other encoding would become unreadable on its first save (#2015).</summary>
    /// <returns><see langword="false"/> when the file is not UTF-8 (a byte order mark is optional);
    /// <paramref name="blockingReason"/> then names its encoding.</returns>
    public static bool CheckEncoding(ReadOnlySpan<byte> bytes, out string? blockingReason)
    {
        var encoding = FindUnsupportedEncoding(bytes);
        blockingReason = encoding is null
            ? null
            : $"the file's encoding is {encoding}. AnimationEditor only supports UTF-8, so convert the file to UTF-8 first.";
        return encoding is null;
    }

    private static string? FindUnsupportedEncoding(ReadOnlySpan<byte> bytes)
    {
        // A byte order mark, or the '<' an XML file starts with. UTF-32 LE also starts like UTF-16 LE.
        if (bytes is [0xFF, 0xFE, 0, 0, ..] or [0, 0, 0xFE, 0xFF, ..] or [(byte)'<', 0, 0, 0, ..] or [0, 0, 0, (byte)'<', ..])
            return "UTF-32";
        if (bytes is [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..] or [(byte)'<', 0, ..] or [0, (byte)'<', ..])
            return "UTF-16";

        if (bytes.StartsWith(Encoding.UTF8.Preamble))
            bytes = bytes[Encoding.UTF8.Preamble.Length..];

        var declarationLength = Math.Max(0, bytes.IndexOf("?>"u8));
        var declared = DeclaredEncoding().Match(Encoding.Latin1.GetString(bytes[..declarationLength]));
        if (declared.Success && !declared.Groups["name"].Value.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
            return declared.Groups["name"].Value;

        return Utf8.IsValid(bytes) ? null : "not valid UTF-8";
    }

    [GeneratedRegex("""^<\?xml\s(.*\s)?encoding\s*=\s*["'](?<name>[^"']*)""", RegexOptions.Singleline)]
    private static partial Regex DeclaredEncoding();

    /// <returns><see langword="false"/> when the editor cannot work with the tileset; <paramref name="blockingReason"/> says why.</returns>
    public static bool CheckOpenCompatibility(Tileset tileset, out string? blockingReason)
    {
        // An image-collection tileset (one image per tile, no shared sheet) has no tile grid for
        // any of this editor's tile-id math to work against.
        if (!tileset.Image.HasValue || tileset.Tiles.Any(t => t.Image.HasValue))
        {
            blockingReason = "the tileset is an image collection (one image per tile), which AnimationEditor doesn't support yet -- only a single-image tileset can be opened.";
            return false;
        }

        blockingReason = null;
        return true;
    }
}
