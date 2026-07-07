using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A view whose zoom a <see cref="ZoomControl"/> can drive and follow. Implemented by
/// <see cref="TextureViewport"/> (wireframe + PNG viewer) and <see cref="PreviewControl"/>,
/// which don't share a base class but expose the same zoom surface.
/// </summary>
public interface IZoomTarget
{
    /// <summary>Current zoom factor (1.0 = 100%).</summary>
    float Zoom { get; }

    /// <summary>Preset percentages the view's mouse-wheel zoom steps through when non-null.</summary>
    int[]? WheelZoomPresets { get; set; }

    /// <summary>Fires after every zoom change; payload is the new zoom as a percentage (100f = 100%).</summary>
    event Action<float>? ZoomChanged;

    /// <summary>Sets the zoom by whole-number percentage (e.g. 100 = 1×).</summary>
    void SetZoomPercent(int percent);
}
