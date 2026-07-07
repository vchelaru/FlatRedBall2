using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState
{
    public interface IApplicationEvents
    {
        event Action AfterZoomChange;
        event Action WireframePanning;
        event Action WireframeTextureChange;
        /// <summary>Fired by <c>AppCommands.OpenAchxWorkflow</c> after the .achx is fully loaded.</summary>
        event Action<string> AchxLoaded;
        event Action<AARectSave> AfterAxisAlignedRectangleChanged;
        event Action<CircleSave> AfterCircleChanged;
        event Action AnimationChainsChanged;
        /// <summary>Fired after the active file path changes (open or save-as). Carries the new path.</summary>
        event Action<string> CurrentFileChanged;
        /// <summary>Fired when texture references in the loaded project may have changed.</summary>
        event Action AvailableTexturesChanged;

        /// <summary>Fired when a referenced PNG changes on disk. Arg: absolute path.</summary>
        event Action<string> PngChangedOnDisk;

        /// <summary>Fired when the .achx file is deleted from disk.</summary>
        event Action<string> AchxDeletedOnDisk;

        /// <summary>Fired after a successful hot-reload of the .achx file. Arg: path.</summary>
        event Action<string> AchxReloadedFromDisk;

        /// <summary>
        /// Fired when a frame is added or retextured. The wireframe uses this to zoom-to-fit the frame
        /// on its next texture load, but only if it's larger than the viewport — so a new/retextured
        /// frame on a large sheet isn't left selected with its edges and handles off-screen (#616).
        /// </summary>
        event Action FitFrameToViewRequested;

        void RaiseAfterAxisAlignedRectangleChanged(AARectSave rectangle);
        void RaiseAfterCircleChanged(CircleSave circle);
        void RaiseAnimationChainsChanged();
        void CallAchxLoaded(string newFileName);
        void CallAfterZoomChange();
        void CallAfterWireframePanning();
        void CallWireframeTextureChange();
        void RaiseCurrentFileChanged(string path);
        void RaiseAvailableTexturesChanged();
        void RaisePngChangedOnDisk(string path);
        void RaiseAchxDeletedOnDisk(string path);
        void RaiseAchxReloadedFromDisk(string path);
        void RaiseFitFrameToViewRequested();
    }
}
