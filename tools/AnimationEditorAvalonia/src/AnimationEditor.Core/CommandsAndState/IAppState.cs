using AnimationEditor.Core.Data;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState
{
    public interface IAppState
    {
        int WireframeZoomValue { get; set; }
        bool IsSnapToGridChecked { get; set; }
        int GridSize { get; set; }
        AnimationFrameSave? CurrentFrame { get; }
        SpriteAlignment SpriteAlignment { get; set; }
        float OffsetMultiplier { get; set; }
    }
}
