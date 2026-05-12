using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState
{
    public interface IApplicationEvents
    {
        event Action AfterZoomChange;
        event Action WireframePanning;
        event Action WireframeTextureChange;
        event Action<string> AchxLoaded;
        event Action<AARectSave> AfterAxisAlignedRectangleChanged;
        event Action<CircleSave> AfterCircleChanged;
        event Action AnimationChainsChanged;

        void RaiseAfterAxisAlignedRectangleChanged(AARectSave rectangle);
        void RaiseAfterCircleChanged(CircleSave circle);
        void RaiseAnimationChainsChanged();
        void CallAchxLoaded(string newFileName);
        void CallAfterZoomChange();
        void CallAfterWireframePanning();
        void CallWireframeTextureChange();
    }
}
