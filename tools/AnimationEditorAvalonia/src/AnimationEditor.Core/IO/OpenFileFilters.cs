using System.Collections.Generic;

namespace AnimationEditor.Core.IO;

/// <summary>One entry in an open dialog's file-type dropdown, e.g. ("Tiled Tileset", ["*.tsx"]).</summary>
public readonly record struct OpenFileFilter(string Description, IReadOnlyList<string> Patterns);

/// <summary>File-type dropdown contents for the open dialogs.</summary>
public static class OpenFileFilters
{
    /// <summary>
    /// File &gt; Load. The first entry is the dropdown default and lists every openable format, so no
    /// format is hidden until the user switches filters; the per-format entries narrow it.
    /// </summary>
    public static IReadOnlyList<OpenFileFilter> AnimationFiles { get; } = new[]
    {
        new OpenFileFilter("All Supported Files", new[] { "*.achx", "*.achj", "*.tsx" }),
        new OpenFileFilter("Animation Chain", new[] { "*.achx", "*.achj" }),
        new OpenFileFilter("Tiled Tileset", new[] { "*.tsx" }),
    };
}
