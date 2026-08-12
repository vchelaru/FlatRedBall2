using AnimationEditor.Core.Rendering;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A view whose pan a pair of <c>ScrollBar</c>s can drive and follow, the pan-axis counterpart to
/// <see cref="IZoomTarget"/>. Implemented by <see cref="TextureViewport"/> (wireframe + PNG viewer)
/// and <see cref="PreviewControl"/>, which don't share a base class but expose the same pan surface.
/// <para>
/// <see cref="SetPanX"/> / <see cref="SetPanY"/> take a raw <b>scrollbar value</b>, not a pan — each
/// implementation applies <see cref="PanScrollBar.PanFromValue"/> itself, so hosts never have to
/// remember which control inverts where.
/// </para>
/// </summary>
public interface IPanScrollTarget
{
    /// <summary>Scrollbar range/thumb for each axis, derived from the current pan and viewport.</summary>
    (ScrollBarRange Horizontal, ScrollBarRange Vertical) GetScrollBarRanges();

    /// <summary>Sets the horizontal pan from a scrollbar value and repaints.</summary>
    void SetPanX(float scrollValue);

    /// <summary>Sets the vertical pan from a scrollbar value and repaints.</summary>
    void SetPanY(float scrollValue);

    /// <summary>Fires whenever the camera or viewport size changes, so the bars can re-sync.</summary>
    event Action? ViewChanged;
}
