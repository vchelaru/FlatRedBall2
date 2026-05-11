using FlatRedBall.Content.Math.Geometry;
using System;

namespace AnimationEditor.Core.CommandsAndState
{
    public interface IApplicationEvents
    {
        event Action AfterZoomChange;
        event Action WireframePanning;
        event Action WireframeTextureChange;
        event Action<string> AchxLoaded;
        event Action<AxisAlignedRectangleSave> AfterAxisAlignedRectangleChanged;
        event Action<CircleSave> AfterCircleChanged;
        event Action AnimationChainsChanged;

        void RaiseAfterAxisAlignedRectangleChanged(AxisAlignedRectangleSave rectangle);
        void RaiseAfterCircleChanged(CircleSave circle);
        void RaiseAnimationChainsChanged();
        void CallAchxLoaded(string newFileName);
        void CallAfterZoomChange();
        void CallAfterWireframePanning();
        void CallWireframeTextureChange();
    }
}
