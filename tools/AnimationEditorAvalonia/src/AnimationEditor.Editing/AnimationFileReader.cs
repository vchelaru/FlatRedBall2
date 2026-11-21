using AnimationEditor.Core;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Tiled;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Reads an .achx, .achj or Tiled .tsx as stored on disk (no coordinate conversion for the first two), for the read-only commands.</summary>
public static class AnimationFileReader
{
    /// <summary>
    /// The parsed file, or null with the reason in <see cref="Error"/>. A .tsx comes back as the editor's native
    /// project would show it, with pixel coordinates; <see cref="OwnerTiles"/> then holds each chain's owner tile and
    /// <see cref="Issues"/> the tileset's own group problems (warnings for <c>check</c>).
    /// </summary>
    public sealed record ReadResult(
        AnimationChainListSave? Document,
        string? Error,
        IReadOnlyDictionary<AnimationChainSave, uint>? OwnerTiles = null,
        IReadOnlyList<CheckIssue>? Issues = null);

    public static ReadResult Read(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".achx" && ext != ".achj" && ext != ".tsx")
            return new(null, $"Only .achx, .achj and .tsx files are supported: {path}");
        if (!File.Exists(path))
            return new(null, $"File not found: {path}");
        if (ext == ".tsx") return ReadTsx(Path.GetFullPath(path), name);
        try
        {
            var doc = ext == ".achj" ? AnimationChainListSave.FromJsonFile(path) : AnimationChainListSave.FromFile(path);
            return new(doc, null);
        }
        catch (Exception ex)
        {
            return new(null, $"Could not read {name}: {ex.Message}");
        }
    }

    /// <summary>Opens the .tsx exactly as the editor does, so an unsupported one is refused with the editor's message.</summary>
    private static ReadResult ReadTsx(string path, string name)
    {
        try
        {
            var project = new ProjectManager();
            project.LoadTsxProject(new FilePath(path));
            var doc = project.AnimationChainListSave!;
            var tileset = TsxLoader.LoadTileset(path);

            // The editor holds a .tsx in UV; every other file reads in pixels here.
            var (width, height) = TiledAnimationToAchjMapper.GetTextureSize(tileset);
            foreach (var frame in doc.AnimationChains.SelectMany(c => c.Frames))
            {
                frame.LeftCoordinate *= width;
                frame.RightCoordinate *= width;
                frame.TopCoordinate *= height;
                frame.BottomCoordinate *= height;
            }
            doc.CoordinateType = TextureCoordinateType.Pixel;
            if (doc.TimeMeasurementUnit == TimeMeasurementUnit.Undefined) doc.TimeMeasurementUnit = TimeMeasurementUnit.Second;

            var owners = new Dictionary<AnimationChainSave, uint>(ReferenceEqualityComparer.Instance);
            foreach (var chain in doc.AnimationChains)
                if (project.GetTsxOwnerTileId(chain) is { } tile) owners[chain] = tile;

            var issues = TsxAnimationValidator.Validate(tileset)
                .Select(i => new CheckIssue(CheckSeverity.Warning, "tsx-group", i.Message, $"tile {i.TileId}"))
                .ToList();
            return new(doc, null, owners, issues);
        }
        catch (Exception ex)
        {
            // NotSupportedException from the checker already reads "Can't open ...: reason".
            return new(null, ex is NotSupportedException or InvalidOperationException ? ex.Message : $"Could not read {name}: {ex.Message}");
        }
    }
}
