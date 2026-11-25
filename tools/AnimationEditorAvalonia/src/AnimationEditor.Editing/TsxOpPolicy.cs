using AnimationEditor.Core;
using AnimationEditor.Core.Tiled;
using AnimationEditor.Core.Utilities;
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
        "set-frame-length", "set-chain-length", "invert-frame-order", "set-frame-region", "set-chain-owner-tile",
    ];

    /// <summary>Why <paramref name="op"/> cannot run on this file, or null when it can.</summary>
    public static EditResult? Check(EditSession session, EditOp op)
    {
        var name = op.Spec.Name;
        if (!session.IsNativeTsx && name == "import-chain" && Path.GetExtension(op.Values["from"]).Equals(".tsx", StringComparison.OrdinalIgnoreCase))
            return Fail(EditErrorCode.WrongFileKind, "import-chain reads an .achx or .achj; chains cannot be imported from a Tiled .tsx.");

        if (!session.IsNativeTsx)
            return name == "set-chain-owner-tile"
                ? Fail(EditErrorCode.WrongFileKind, "set-chain-owner-tile applies to a Tiled .tsx; an .achx/.achj has no owner tiles.")
                : null;

        if (!Supported.Contains(name))
            return Fail(EditErrorCode.NotSupportedInTsx, name switch
            {
                "set-chain-locked" => "Locking is editor state that a Tiled .tsx cannot store, so set-chain-locked has no effect on the file.",
                "set-unit" => "A Tiled .tsx stores durations in milliseconds and has no time unit setting.",
                "import-chain" => "import-chain works between .achx/.achj files; chains cannot be imported into a Tiled .tsx.",
                "set-frame-texture" => "A Tiled tileset has one image, so a frame's texture cannot change.",
                _ => $"{TsxEdit.Stores}, so {name} cannot be kept in a .tsx (the editor disables it for tilesets too).",
            });

        if (name is "add-frame" or "add-frames-grid")
        {
            if (name == "add-frame" && !op.Values.ContainsKey("region"))
                return Fail(EditErrorCode.NotSupportedInTsx,
                    "A Tiled tile animation frame is a tile cell, not the whole image: add-frame on a .tsx needs a region.");
            if (TsxEdit.CheckImage(session.ProjectManager, op.Values["texture"]) is { } refused)
                return refused;
        }
        return null;
    }

    /// <summary>The state of a .tsx before the ops, so only problems the ops cause are reported.</summary>
    /// <param name="Unwritable">The chains a save already skips, with what they hold.</param>
    public sealed record Snapshot(IReadOnlyDictionary<AnimationChainSave, string> Unwritable, IReadOnlySet<uint> SharedTiles);

    public static Snapshot Take(EditSession session)
    {
        var unwritable = new Dictionary<AnimationChainSave, string>(ReferenceEqualityComparer.Instance);
        foreach (var chain in Skipped(session))
            unwritable[chain.SourceChain] = Content(session, chain.SourceChain);
        return new Snapshot(unwritable, SharedTiles(session));
    }

    /// <summary>
    /// Errors for changed chains the save would skip: frames that do not line up with whole tiles, or a tile two
    /// chains share (the save would keep one and skip the other with only a warning).
    /// </summary>
    public static EditResult? CheckWritable(EditSession session, Snapshot before)
    {
        var newlyShared = SharedTiles(session).Where(t => !before.SharedTiles.Contains(t)).ToList();
        var errors = new List<string>();
        var codes = new List<EditErrorCode>();
        foreach (var result in Skipped(session))
        {
            var chain = result.SourceChain;
            if (before.Unwritable.TryGetValue(chain, out var content) && content == Content(session, chain)) continue;
            if (session.ProjectManager.GetTsxOwnerTileId(chain) is { } owner && newlyShared.Contains(owner)) continue; // reported below

            errors.Add($"Chain '{chain.Name}' cannot be written to the .tsx: {result.SkipReason}.");
            codes.Add(result.TileHolder is null ? EditErrorCode.UnmappableChain : EditErrorCode.DuplicateOwnerTile);
        }
        foreach (var tile in newlyShared)
        {
            var names = session.Document.AnimationChains
                .Where(c => session.ProjectManager.GetTsxOwnerTileId(c) == tile).Select(c => $"'{c.Name}'");
            errors.Add($"Chains {string.Join(" and ", names)} both map to tile {tile}, so only one could be written; give one its own with set-chain-owner-tile.");
            codes.Add(EditErrorCode.DuplicateOwnerTile);
        }
        return errors.Count == 0 ? null : new EditResult([], errors, [], codes);
    }

    private static IEnumerable<MultiTileMappingResult> Skipped(EditSession session) =>
        session.ProjectManager.MapTsxForSave().Where(r => r.SkipReason is not null);

    private static string Content(EditSession session, AnimationChainSave chain) => string.Join("\n",
    [
        chain.Name, $"{session.ProjectManager.GetTsxOwnerTileId(chain)}",
        .. chain.Frames.Select(f => FormattableString.Invariant($"{FrameRegion.Of(f)} {f.FrameLength:R} {f.TextureName}")),
    ]);

    private static HashSet<uint> SharedTiles(EditSession session) =>
        session.Document.AnimationChains.Select(session.ProjectManager.GetTsxOwnerTileId).OfType<uint>()
            .GroupBy(tile => tile).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
