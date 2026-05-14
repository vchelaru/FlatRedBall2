using AnimationEditor.Core.Data;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState
{
    public interface IAppState
    {
        string? ProjectFolder { get; set; }
        int WireframeZoomValue { get; set; }
        bool IsSnapToGridChecked { get; set; }
        int GridSize { get; set; }
        AnimationFrameSave? CurrentFrame { get; }
        SpriteAlignment SpriteAlignment { get; set; }
        float OffsetMultiplier { get; set; }
    }
}
