using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState
{
    public class ApplicationEvents : IApplicationEvents
    {
        public event Action? AfterZoomChange;
        public event Action? WireframePanning;
        public event Action? WireframeTextureChange;
        public event Action<string>? AchxLoaded;
        public event Action<AARectSave>? AfterAxisAlignedRectangleChanged;
        public event Action<CircleSave>? AfterCircleChanged;
        public event Action? AnimationChainsChanged;
        public event Action<string>? CurrentFileChanged;
        public event Action? AvailableTexturesChanged;
        public event Action<string>? PngChangedOnDisk;
        public event Action<string>? AchxDeletedOnDisk;
        public event Action<string>? AchxReloadedFromDisk;

        public void RaiseAfterAxisAlignedRectangleChanged(AARectSave rectangle) =>
            AfterAxisAlignedRectangleChanged?.Invoke(rectangle);

        public void RaiseAfterCircleChanged(CircleSave circle) =>
            AfterCircleChanged?.Invoke(circle);

        public void RaiseAnimationChainsChanged() =>
            AnimationChainsChanged?.Invoke();

        public void CallAchxLoaded(string newFileName) =>
            AchxLoaded?.Invoke(newFileName);

        public void CallAfterZoomChange() =>
            AfterZoomChange?.Invoke();

        public void CallAfterWireframePanning() =>
            WireframePanning?.Invoke();

        public void CallWireframeTextureChange() =>
            WireframeTextureChange?.Invoke();

        public void RaiseCurrentFileChanged(string path) =>
            CurrentFileChanged?.Invoke(path);

        public void RaiseAvailableTexturesChanged() =>
            AvailableTexturesChanged?.Invoke();

        public void RaisePngChangedOnDisk(string path) =>
            PngChangedOnDisk?.Invoke(path);

        public void RaiseAchxDeletedOnDisk(string path) =>
            AchxDeletedOnDisk?.Invoke(path);

        public void RaiseAchxReloadedFromDisk(string path) =>
            AchxReloadedFromDisk?.Invoke(path);
    }
}
