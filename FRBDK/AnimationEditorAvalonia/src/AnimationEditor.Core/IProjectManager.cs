using AnimationEditor.Core.Data;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Graphics;
using FlatRedBall.IO;

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
