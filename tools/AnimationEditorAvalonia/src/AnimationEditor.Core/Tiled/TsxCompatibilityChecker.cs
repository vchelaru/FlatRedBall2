using DotTiled;
using System;
using System.IO;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Checks whether a Tiled <see cref="Tileset"/> can be opened as a native AnimationEditor project
/// (issue #1140) without losing data on the first save. Deliberately reuses <see
/// cref="TsxWriter"/> itself as the source of truth (a dry-run write to a throwaway stream) rather
/// than duplicating its wangsets/transformations/object-layer/property-type checks -- so this stays
/// in sync automatically if <see cref="TsxWriter"/>'s supported subset ever changes.
/// </summary>
public static class TsxCompatibilityChecker
{
    /// <returns><see langword="false"/> when opening natively would crash on the first save;
    /// <paramref name="blockingReason"/> is <see cref="TsxWriter"/>'s own explanation in that case.</returns>
    public static bool CheckOpenCompatibility(Tileset tileset, out string? blockingReason)
    {
        try
        {
            TsxWriter.Write(tileset, Stream.Null);
            blockingReason = null;
            return true;
        }
        catch (NotSupportedException ex)
        {
            blockingReason = ex.Message;
            return false;
        }
    }
}
