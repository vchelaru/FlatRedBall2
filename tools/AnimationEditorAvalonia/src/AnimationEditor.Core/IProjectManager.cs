using AnimationEditor.Core.Data;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core
{
    public interface IProjectManager
    {
        AnimationChainListSave? AnimationChainListSave { get; set; }
        TileMapInformationList TileMapInformationList { get; set; }
        FilePath[] ReferencedPngs { get; set; }
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
        TileGrid? TsxTileGrid { get; }

        void LoadAnimationChain(
            FilePath fileName,
            AnimationChainListSave? preParsed = null,
            IReadOnlyDictionary<string, (int Width, int Height)>? knownTextureSizes = null);
        void SaveAnimationChainList(string targetPath);

        /// <summary>
        /// Writes <paramref name="document"/>, which need not be the current one (a background
        /// tab's model after a cross-document cut), to <paramref name="targetPath"/> in
        /// <paramref name="diskFormat"/>. The in-memory document stays in UV afterwards, as the
        /// current-model save leaves it.
        /// </summary>
        void SaveAnimationChainList(AnimationChainListSave document, string targetPath, TextureCoordinateType diskFormat);

        /// <summary>Opens <paramref name="fileName"/> as a native AnimationEditor project -- see
        /// <see cref="ProjectManager.LoadTsxProject"/>.</summary>
        void LoadTsxProject(FilePath fileName);

        /// <summary>Saves back to the tsx opened by <see cref="LoadTsxProject"/>; no-op (returns
        /// empty) if none is loaded. Returns every mapping warning from this save (e.g. a chain
        /// whose frame geometry couldn't be written this time) so a caller can surface them --
        /// the chain's previously-written tile is left untouched, not cleared, when this is
        /// non-empty. See <see cref="ProjectManager.SaveTsxProject"/>.</summary>
        IReadOnlyList<string> SaveTsxProject(string? targetPath = null);

        /// <summary>Names of chains with a tsx validation issue; empty when no tsx project is
        /// loaded or nothing is wrong. See <see cref="ProjectManager.GetChainNamesWithTsxIssues"/>.</summary>
        IReadOnlyList<string> GetChainNamesWithTsxIssues();

        /// <summary>The tile id a native-tsx chain's animation would be written to on the next
        /// save. See <see cref="ProjectManager.GetTsxOwnerTileId"/>.</summary>
        uint? GetTsxOwnerTileId(AnimationChainSave chain);

        /// <summary>Explicitly overrides which tile id a native-tsx chain's animation is written
        /// to. See <see cref="ProjectManager.TrySetTsxOwnerTileId"/>.</summary>
        string? TrySetTsxOwnerTileId(AnimationChainSave chain, uint tileId);

        /// <summary>True when calling <see cref="TrySetTsxOwnerTileId"/> with these exact
        /// arguments would be a pure no-op -- the chain is already pinned to <paramref
        /// name="tileId"/> with no satellite hints left to drop. See <see
        /// cref="ProjectManager.IsTsxOwnerTileIdAlreadySet"/>.</summary>
        bool IsTsxOwnerTileIdAlreadySet(AnimationChainSave chain, uint tileId);

        /// <summary>Marks a chain's name as real (no longer the synthetic "ID:{tileId}"
        /// placeholder). See <see cref="ProjectManager.MarkChainNameExplicit"/>.</summary>
        bool MarkChainNameExplicit(AnimationChainSave chain);

        /// <summary>True when a native-tsx chain's name is the synthetic "ID:{tileId}" placeholder
        /// (no <c>Name</c> property in the tsx). Always false for an achx/achj project.</summary>
        bool IsChainNameAuto(AnimationChainSave chain);

        /// <summary>Reverts a native-tsx chain to its synthetic name. See <see
        /// cref="ProjectManager.MakeChainNameAuto"/>.</summary>
        bool MakeChainNameAuto(AnimationChainSave chain);

        /// <summary>The Tiled tile id a frame's own pixel rect resolves to. See <see
        /// cref="ProjectManager.ComputeFrameTileId"/>.</summary>
        uint? ComputeFrameTileId(AnimationFrameSave frame);

        /// <summary>
        /// Captures this project's native-tsx state (tileset + tile-id tracking dictionaries) as
        /// an opaque snapshot, or <see langword="null"/> for an achx/achj project. <see
        /// cref="AnimationEditor.Core.Models.TabEditorCache"/> uses this to round-trip a tab's tsx
        /// identity across a cache-hit tab switch (<c>TryActivateTabFromCache</c>) -- that path
        /// never calls <see cref="LoadTsxProject"/>/<see cref="LoadAnimationChain"/>, so without
        /// this, <see cref="IsNativeTsxProject"/>/<see cref="TsxTileGrid"/> keep reflecting
        /// whichever tab was most recently loaded from disk instead of the tab being switched to.
        /// </summary>
        object? CaptureTsxState();

        /// <summary>
        /// Restores a snapshot previously returned by <see cref="CaptureTsxState"/> on this same
        /// instance, or clears all native-tsx state when <paramref name="state"/> is <see
        /// langword="null"/> (restoring an achx/achj tab). Passing a snapshot captured from a
        /// different <see cref="IProjectManager"/> instance is undefined.
        /// </summary>
        void RestoreTsxState(object? state);

        /// <summary>
        /// Captures the texture sizes supplied to the most recent <see cref="LoadAnimationChain"/>
        /// call as an opaque snapshot, or <see langword="null"/> if none were supplied. Same
        /// tab-switch-cache shape as <see cref="CaptureTsxState"/>: without this,
        /// <see cref="AnimationEditor.Core.Models.TabEditorCache"/>'s cache-hit tab switch
        /// (<c>TryActivateTabFromCache</c>) leaves this project's known texture sizes at
        /// whichever tab was most recently loaded from disk, so <see
        /// cref="SaveAnimationChainList(Stream)"/> on the reactivated tab converts back to Pixel
        /// using the wrong (or missing) sizes on the browser-wasm build, which has no filesystem
        /// to fall back to.
        /// </summary>
        object? CaptureTextureSizeState();

        /// <summary>
        /// Restores a snapshot previously returned by <see cref="CaptureTextureSizeState"/> on
        /// this same instance, or clears the known texture sizes when <paramref name="state"/> is
        /// <see langword="null"/>.
        /// </summary>
        void RestoreTextureSizeState(object? state);

        /// <summary>
        /// Resets this instance to a brand-new, unsaved, non-tsx document: a fresh empty <see
        /// cref="AnimationChainListSave"/>, <see cref="FileName"/> cleared to <see
        /// langword="null"/>, <see cref="OnDiskCoordinateType"/> back to its Pixel default, and
        /// every native-tsx/texture-size/<see cref="ReferencedPngs"/> tracking field cleared (the
        /// same reset <see cref="RestoreTsxState"/>/<see cref="RestoreTextureSizeState"/> apply
        /// individually) -- one call instead of repeating that field list at every "start a fresh
        /// document" call site (issue #1147). Leaves <see cref="ProjectFolderPath"/>, tab/undo/
        /// selection state untouched; callers own those.
        /// </summary>
        void ResetToBlankDocument();

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
