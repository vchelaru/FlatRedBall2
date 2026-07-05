using AnimationEditor.Core.Data;
using FlatRedBall2.Animation.Content;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core
{
    public interface IProjectManager
    {
        AnimationChainListSave? AnimationChainListSave { get; set; }
        TileMapInformationList TileMapInformationList { get; set; }
        FilePath[] ReferencedPngs { get; }
        string? FileName { get; set; }
        TextureCoordinateType OnDiskCoordinateType { get; set; }

        void LoadAnimationChain(FilePath fileName, AnimationChainListSave? preParsed = null);
        void SaveAnimationChainList(string targetPath);

        /// <summary>
        /// Root folder the Files panel should browse: the linked project's Content folder
        /// when it resolves, otherwise the folder containing the loaded .achx.
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
