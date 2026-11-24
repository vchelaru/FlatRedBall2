using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Tiled tileset ops (#1811): associating a .tsx with an .achx/.achj (the editor's achx-push feature) and choosing
/// which tile owns a native-.tsx chain's animation. Never touches selection; an owner change is one undo step.
/// </summary>
public static class TsxEdit
{
    /// <summary>
    /// Moves a native-.tsx chain's animation to <paramref name="tileId"/>. Fails with
    /// <see cref="EditErrorCode.InvalidValue"/> (the project's own reason) for a tile past the tileset or owned by
    /// another chain, and with <see cref="EditErrorCode.WrongFileKind"/> outside a native .tsx.
    /// </summary>
    public static EditResult SetChainOwnerTile(IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, uint tileId)
    {
        if (!projectManager.IsNativeTsxProject) return NotTsx();
        if (projectManager.IsTsxOwnerTileIdAlreadySet(chain, tileId)) return new([], [], []);

        var error = commands.SetChainTsxOwnerTileId(chain, tileId);
        return error is null ? new([chain.Name], [], []) : Fail(EditErrorCode.InvalidValue, error);
    }

    /// <summary>Returns the chain's owner to the tile of its first frame, the editor's "Sync to First Frame".</summary>
    public static EditResult ClearChainOwnerTile(IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain)
    {
        if (!projectManager.IsNativeTsxProject) return NotTsx();
        if (chain.Frames.Count == 0)
            return Fail(EditErrorCode.InvalidValue, $"Chain '{chain.Name}' has no frames, so there is no first-frame tile to return to.");
        if (projectManager.ComputeFrameTileId(chain.Frames[0]) is not { } tile)
            return Fail(EditErrorCode.InvalidValue,
                $"The first frame of '{chain.Name}' is not one whole tile, so there is no single tile to return to; give one with tile.");
        return SetChainOwnerTile(projectManager, commands, chain, tile);
    }

    /// <summary>
    /// Checks that <paramref name="tsxPath"/> can be associated with the open .achx/.achj, without writing anything.
    /// </summary>
    public static EditResult CheckAssociation(IProjectManager projectManager, string tsxPath)
    {
        if (projectManager.IsNativeTsxProject)
            return Fail(EditErrorCode.WrongFileKind, ProjectManager.NativeTsxAssociationRefusal);
        if (!string.Equals(Path.GetExtension(tsxPath), ".tsx", StringComparison.OrdinalIgnoreCase))
            return Fail(EditErrorCode.InvalidValue, $"'{tsxPath}' is not a .tsx file.");
        if (!File.Exists(tsxPath))
            return Fail(EditErrorCode.NotFound, $"Tileset not found: {tsxPath}");
        try
        {
            TsxLoader.LoadTileset(tsxPath);
        }
        catch (Exception ex)
        {
            return Fail(EditErrorCode.InvalidValue, $"Could not read tileset {Path.GetFileName(tsxPath)}: {ex.Message}");
        }
        return new([tsxPath], [], []);
    }

    /// <summary>
    /// Records <paramref name="tsxPath"/> in the open file's Tiled-sync companion file, so every later save of the
    /// .achx/.achj pushes its animations onto that tileset. Not undoable, and it writes immediately; the sync itself
    /// happens on the next save.
    /// </summary>
    public static EditResult AssociateTileset(IAppCommands commands, IProjectManager projectManager, string tsxPath)
    {
        tsxPath = Path.GetFullPath(tsxPath);
        var check = CheckAssociation(projectManager, tsxPath);
        if (!check.Success) return check;

        try
        {
            commands.AddAssociatedTiledTileset(tsxPath);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(EditErrorCode.InvalidValue, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return Fail(EditErrorCode.IoFailed, ex.Message);
        }
        return check;
    }

    private static EditResult NotTsx() =>
        Fail(EditErrorCode.WrongFileKind, "Owner tiles exist only in a Tiled .tsx opened as a native project, not in an .achx/.achj.");

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
