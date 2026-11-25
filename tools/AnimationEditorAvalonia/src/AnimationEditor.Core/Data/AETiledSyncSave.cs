using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Data
{
    /// <summary>
    /// Project configuration for Tiled tileset sync: the <c>.tsx</c> files this .achx/.achj's
    /// animation chains should be pushed into on save (issue #1133). Persisted as a dedicated
    /// <c>.tiledsync</c> companion file next to the achx/achj -- deliberately separate from
    /// <see cref="AESettingsSave"/>'s <c>.aeproperties</c> file, whose established contract is
    /// personal editor view-state. Unlike that file, a <c>.tiledsync</c> file is project
    /// configuration and should be committed and shared with the team (issue #1136). Serialized
    /// as JSON via <see cref="AETiledSyncJsonContext"/>, not XML -- new companion file formats in
    /// this tool are JSON going forward.
    /// </summary>
    public class AETiledSyncSave
    {
        public List<string> TiledTilesetPaths { get; set; } = new List<string>();

        /// <summary>The associated tilesets, whose paths are stored relative to <paramref name="folder"/> (the one this file is
        /// in), or absolute from another drive or network share.</summary>
        public IEnumerable<FilePath> ResolveTilesets(FilePath folder) =>
            TiledTilesetPaths.Select(path => new FilePath(TexturePathHelper.ResolveDisplayPath(path, folder.FullPath)));
    }
}
