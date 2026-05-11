using AnimationEditor.Core.Data;
using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState
{
    public interface IAppState
    {
        string? ProjectFolder { get; set; }
        UnitType UnitType { get; set; }
        int WireframeZoomValue { get; set; }
        bool IsSnapToGridChecked { get; set; }
        int GridSize { get; set; }
        AnimationFrameSave? CurrentFrame { get; }
        SpriteAlignment SpriteAlignment { get; set; }
        float OffsetMultiplier { get; set; }
    }
}
