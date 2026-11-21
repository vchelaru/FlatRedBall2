using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// What an ops file may do to a Tiled .tsx opened as a native project (#1811), and the checks that keep an edit
/// from being silently dropped on save. A Tiled tile animation holds only frame rectangles and durations, so the
/// editor disables everything else for a tileset; the same ops fail here with <see cref="EditErrorCode.NotSupportedInTsx"/>.
/// </summary>
internal static class TsxOpPolicy
{
    /// <summary>Ops that work on a .tsx; <see cref="EditOps.TsxNote"/> lists them for <c>--schema</c>.</summary>
    public static readonly IReadOnlyList<string> Supported =
    [
        "rename-chain", "add-chain", "delete-chain", "duplicate-chain", "reorder-chain",
        "add-frame", "add-frames-grid", "delete-frame", "duplicate-frame", "reorder-frame",
        "set-frame-length", "set-frame-region", "set-chain-owner-tile",
    ];

    private const string Stores = "A Tiled tile animation holds only frame rectangles and durations";

    /// <summary>Why <paramref name="op"/> cannot run on this file, or null when it can.</summary>
    public static EditResult? Check(EditSession session, EditOp op)
    {
        var name = op.Spec.Name;
        if (!session.IsNativeTsx)
            return name == "set-chain-owner-tile"
                ? Fail(EditErrorCode.WrongFileKind, "set-chain-owner-tile applies to a Tiled .tsx; an .achx/.achj has no owner tiles.")
                : null;

        if (name == "associate-tileset")
            return Fail(EditErrorCode.WrongFileKind,
                "associate-tileset applies to an .achx/.achj; a .tsx cannot be a native project and an association target at once.");

        if (!Supported.Contains(name))
            return Fail(EditErrorCode.NotSupportedInTsx, name switch
            {
                "set-chain-locked" => "Locking is editor state that a Tiled .tsx cannot store, so set-chain-locked has no effect on the file.",
                "set-unit" => "A Tiled .tsx stores durations in milliseconds and has no time unit setting.",
                "set-frame-texture" => "A Tiled tileset has one image, so a frame's texture cannot change.",
                _ => $"{Stores}, so {name} cannot be kept in a .tsx (the editor disables it for tilesets too).",
            });

        if (name is "add-frame" or "add-frames-grid")
        {
            if (name == "add-frame" && !op.Values.ContainsKey("region"))
                return Fail(EditErrorCode.NotSupportedInTsx,
                    "A Tiled tile animation frame is a tile cell, not the whole image: add-frame on a .tsx needs a region.");
            if (!IsTilesetImage(session, op.Values["texture"]))
                return Fail(EditErrorCode.NotSupportedInTsx, "A Tiled tileset has one image, so frames can only use the tileset's own image.");
        }
        return null;
    }

    private static bool IsTilesetImage(EditSession session, string texture)
    {
        var folder = Path.GetDirectoryName(session.Path) ?? string.Empty;
        var absolute = TexturePathHelper.ResolveDisplayPath(texture, folder);
        var image = session.ProjectManager.ReferencedPngs.FirstOrDefault();
        return image is not null && new FilePath(absolute) == image;
    }

    /// <summary>The state of a .tsx before the ops, so only problems the ops cause are reported.</summary>
    public sealed record Snapshot(IReadOnlySet<AnimationChainSave> Unmappable, IReadOnlySet<uint> SharedTiles);

    public static Snapshot Take(EditSession session)
    {
        var (unmappable, shared) = Scan(session);
        return new Snapshot(unmappable, shared);
    }

    /// <summary>
    /// Errors for chains the save could not write: frames that do not line up with whole tiles, or an owner tile two
    /// chains share (the save would keep one and skip the other with only a warning).
    /// </summary>
    public static EditResult? CheckWritable(EditSession session, Snapshot before)
    {
        var (unmappable, shared) = Scan(session);
        var errors = new List<string>();
        var codes = new List<EditErrorCode>();
        foreach (var chain in unmappable.Where(c => !before.Unmappable.Contains(c)))
        {
            errors.Add($"Chain '{chain.Name}' cannot be written to the .tsx: its frames do not line up with whole tiles of the tileset.");
            codes.Add(EditErrorCode.UnmappableChain);
        }
        foreach (var tile in shared.Where(t => !before.SharedTiles.Contains(t)))
        {
            var names = session.Document.AnimationChains
                .Where(c => session.ProjectManager.GetTsxOwnerTileId(c) == tile).Select(c => $"'{c.Name}'");
            errors.Add($"Chains {string.Join(" and ", names)} both map to tile {tile}, so only one could be written; give one its own with set-chain-owner-tile.");
            codes.Add(EditErrorCode.DuplicateOwnerTile);
        }
        return errors.Count == 0 ? null : new EditResult([], errors, [], codes);
    }

    private static (HashSet<AnimationChainSave> Unmappable, HashSet<uint> Shared) Scan(EditSession session)
    {
        var unmappable = new HashSet<AnimationChainSave>(ReferenceEqualityComparer.Instance);
        var owners = new Dictionary<uint, int>();
        foreach (var chain in session.Document.AnimationChains)
        {
            if (session.ProjectManager.GetTsxOwnerTileId(chain) is { } tile)
                owners[tile] = owners.GetValueOrDefault(tile) + 1;
            else if (chain.Frames.Count > 0)
                unmappable.Add(chain);
        }
        return (unmappable, owners.Where(p => p.Value > 1).Select(p => p.Key).ToHashSet());
    }

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
