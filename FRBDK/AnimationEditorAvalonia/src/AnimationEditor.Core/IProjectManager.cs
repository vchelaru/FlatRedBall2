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

        void LoadAnimationChain(FilePath fileName);
        void SaveAnimationChainList(string targetPath);
    }
}
