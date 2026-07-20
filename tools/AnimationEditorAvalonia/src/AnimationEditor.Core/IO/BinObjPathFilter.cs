using System;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Pure filter for the "Exclude bin/obj folders" checkbox in the Project tab (issue #770).
/// MSBuild copies content into <c>bin</c>/<c>obj</c> during build, producing duplicate-looking
/// <c>.achx</c> entries alongside the real source ones when a project root is picked.
/// </summary>
public static class BinObjPathFilter
{
    /// <summary>
    /// True when any '/'-or-'\'-separated segment of <paramref name="relativePath"/> is
    /// case-insensitively equal to "bin" or "obj" (a substring match like "Cabin" does not count).
    /// </summary>
    public static bool IsExcluded(string relativePath)
    {
        foreach (var segment in relativePath.Split('/', '\\'))
        {
            if (segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
