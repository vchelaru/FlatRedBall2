using AnimationEditor.Core.Data;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core
{
    public interface IProjectManager
    {
        AnimationChainListSave? AnimationChainListSave { get; set; }
        TileMapInformationList TileMapInformationList { get; set; }
        FilePath[] ReferencedPngs { get; }
        string? FileName { get; set; }

        /// <summary>
        /// The folder explicitly picked via File → Open Project Folder (or restored at startup).
        /// Never inferred from the open .achx -- see <see cref="ProjectManager.ProjectFolderPath"/>.
        /// </summary>
        string? ProjectFolderPath { get; set; }

        TextureCoordinateType OnDiskCoordinateType { get; set; }

        /// <summary>Whether the currently loaded project is a native <c>.tsx</c> project (see
        /// <see cref="LoadTsxProject"/>) rather than an achx/achj project.</summary>
        bool IsNativeTsxProject { get; }

        /// <summary>The tsx's own fixed tile size, or <see langword="null"/> for an achx/achj
        /// project. Not user-configurable for a native tsx project (issue #1140).</summary>
        (int Width, int Height)? TsxTileSize { get; }

        void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null);
        void SaveAnimationChainList(string targetPath);

        /// <summary>Opens <paramref name="fileName"/> as a native AnimationEditor project -- see
        /// <see cref="ProjectManager.LoadTsxProject"/>.</summary>
        void LoadTsxProject(FilePath fileName);

        /// <summary>Saves back to the tsx opened by <see cref="LoadTsxProject"/>; no-op if none is
        /// loaded. See <see cref="ProjectManager.SaveTsxProject"/>.</summary>
        void SaveTsxProject(string? targetPath = null);

        /// <summary>Names of chains with a tsx validation issue; empty when no tsx project is
        /// loaded or nothing is wrong. See <see cref="ProjectManager.GetChainNamesWithTsxIssues"/>.</summary>
        IReadOnlyList<string> GetChainNamesWithTsxIssues();

        /// <summary>
        /// Stream-based counterpart to <see cref="SaveAnimationChainList(string)"/> for platforms
        /// with no filesystem path to write (the browser-wasm build). Uses the
        /// <c>knownTextureSizes</c> from the most recent <see cref="LoadAnimationChain"/> call
        /// instead of a disk read when converting back to Pixel coordinates.
        /// </summary>
        void SaveAnimationChainList(Stream stream);

        /// <summary>
        /// Root folder the Files panel should browse: the linked project's folder (if the
        /// <c>ProjectFile</c> reference resolves to an existing directory) or its <c>Content</c>
        /// subfolder, then the nearest ancestor named <c>Content</c> walking up from the .achx's
        /// folder, then the .achx's own folder.
        /// </summary>
        string? ResolveFilesPanelRoot();

        /// <summary>
        /// Resolves a frame's texture name to its pixel size (PNG header read), relative to the
        /// loaded .achx directory. Returns <c>null</c> when the name is empty or unreadable.
        /// </summary>
        (int Width, int Height)? GetTextureSizeInPixels(string textureName);

        /// <summary>
        /// Returns the texture names referenced by <paramref name="acls"/> that cannot be
        /// decoded from <paramref name="achxDirectory"/>. An empty list means all textures
        /// are present and valid. Only non-empty texture names are checked; each unique name
        /// is checked at most once.
        /// </summary>
        IReadOnlyList<string> FindMissingTextures(AnimationChainListSave acls, string achxDirectory);
    }
}
