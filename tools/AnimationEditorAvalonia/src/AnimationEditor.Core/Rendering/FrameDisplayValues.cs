using FlatRedBall2.AnimationEditorCommon;
using System;

namespace AnimationEditor.Core.Rendering
{
    /// <summary>
    /// A frame's pixel region as the whole numbers the property inspector shows and the preview draws.
    /// </summary>
    public static class FrameDisplayValues
    {
        /// <summary>Pixel X of the frame's left edge.</summary>
        public static int GetPixelX(AnimationFrameSave frame)
            => (int)Math.Round(frame.LeftCoordinate);

        /// <summary>Pixel Y of the frame's top edge.</summary>
        public static int GetPixelY(AnimationFrameSave frame)
            => (int)Math.Round(frame.TopCoordinate);

        /// <summary>Pixel width of the frame region (minimum 1).</summary>
        public static int GetPixelWidth(AnimationFrameSave frame)
            => Math.Max(1, (int)Math.Round(frame.RightCoordinate - frame.LeftCoordinate));

        /// <summary>Pixel height of the frame region (minimum 1).</summary>
        public static int GetPixelHeight(AnimationFrameSave frame)
            => Math.Max(1, (int)Math.Round(frame.BottomCoordinate - frame.TopCoordinate));

    }
}
