using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Tiled;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Tiled tileset ops (#1811): choosing which tile owns a native-.tsx chain's animation. Never touches selection; an
/// owner change is one undo step.
/// </summary>
public static class TsxEdit
{
    internal const string Stores = "A Tiled tile animation holds only frame rectangles and durations";

    /// <summary>Why a frame of a .tsx takes no other texture.</summary>
    public const string OtherImageRefusal = "A Tiled tileset has one image, so frames can only use the tileset's own image.";

    /// <summary>Why collision shapes cannot be added to a frame of a .tsx.</summary>
    public const string ShapesRefusal = Stores + ", so collision shapes cannot be kept in a .tsx.";

    /// <summary>The refusal when <paramref name="texture"/> is not the open .tsx's image; null when it is, or when no .tsx is open.</summary>
    public static EditResult? CheckImage(IProjectManager projectManager, string texture) =>
        projectManager.TsxTileset is { } tileset && !tileset.IsImage(texture)
            ? Fail(EditErrorCode.NotSupportedInTsx, OtherImageRefusal)
            : null;

    /// <summary>The refusal when <paramref name="frames"/> differ in size from the frames of <paramref name="chain"/> in an open .tsx.</summary>
    public static EditResult? CheckFrameSize(IProjectManager projectManager, AnimationChainSave chain, IEnumerable<AnimationFrameSave> frames) =>
        TsxFrameSize.Refusal(projectManager, chain, frames) is { } reason ? Fail(EditErrorCode.NotSupportedInTsx, reason) : null;

    /// <summary>The same for a new frame showing <paramref name="added"/>.</summary>
    public static EditResult? CheckFrameSize(IProjectManager projectManager, AnimationChainSave chain, FrameRegion added) =>
        TsxFrameSize.Refusal(projectManager, chain, [added]) is { } reason ? Fail(EditErrorCode.NotSupportedInTsx, reason) : null;

    /// <summary>The refusal when moving <paramref name="frames"/> into <paramref name="target"/> is an edit an open .tsx could not store.</summary>
    public static EditResult? CheckFrameMove(
        IProjectManager projectManager, AnimationChainSave target, IReadOnlyList<AnimationFrameSave> frames, int insertIndex) =>
        TsxFrameMove.Refusal(projectManager, target, frames, insertIndex) is { } reason ? Fail(EditErrorCode.NotSupportedInTsx, reason) : null;

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

    private static EditResult NotTsx() =>
        Fail(EditErrorCode.WrongFileKind, "Owner tiles exist only in a Tiled .tsx opened as a native project, not in an .achx/.achj.");

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
