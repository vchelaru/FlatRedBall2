using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Resolves the pixel size of each texture name via an arbitrary async name-based lookup,
/// skipping any name the lookup can't resolve instead of failing the whole batch. Backs the
/// #763 fallback load path (targeted <c>getFileHandle(name)</c> fetches when directory
/// enumeration throws) so a texture the lookup can't reach -- e.g. living outside a browser Open
/// Folder grant, the separate cross-folder-texture problem -- is silently omitted rather than
/// aborting the load.
/// </summary>
public static class NamedTextureResolver
{
    public static async Task<Dictionary<string, (int Width, int Height)>> ResolveSizesAsync(
        IEnumerable<string> textureNames,
        Func<string, Task<(int Width, int Height)?>> tryGetSize)
    {
        var result = new Dictionary<string, (int Width, int Height)>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in textureNames)
        {
            var size = await tryGetSize(name);
            if (size is not null)
                result[name] = size.Value;
        }
        return result;
    }
}
