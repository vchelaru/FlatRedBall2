using AnimationEditor.Core.Input;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A canvas whose mouse wheel, touchpad scroll and pinch go through <see cref="PanZoomWheelInput"/>:
/// the sibling of <see cref="IZoomTarget"/> and <see cref="IPanScrollTarget"/> for the host's OS-specific
/// touchpad detector. Implemented by <see cref="TextureViewport"/> and <see cref="PreviewControl"/>.
/// </summary>
public interface IWheelInputTarget
{
    /// <summary>
    /// Tells a touchpad scroll from a mouse wheel. Defaults to treating every event as a mouse wheel,
    /// which keeps the browser head and standalone controls zooming; <c>MainWindow</c> sets the
    /// detector for the running OS.
    /// </summary>
    IWheelSourceDetector WheelSourceDetector { get; set; }

    /// <summary>Moves the content by a pan in device-independent pixels, clamped to the pan band.</summary>
    void PanBy(float dx, float dy);
}
