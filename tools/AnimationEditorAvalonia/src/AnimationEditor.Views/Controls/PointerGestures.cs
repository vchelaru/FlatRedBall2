using Avalonia.Input;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Shared pointer-gesture logic for every zoomable/pannable panel (TextureViewport --
/// wireframe and the PNG diff pane -- and PreviewControl). These controls don't share a base
/// class (see the "zoom hosts share no base class" landmine), so without a single source of
/// truth here each one's OnPointerPressed silently re-derives its own copy of "is this a pan"
/// and can drift from the others -- which is exactly how PreviewControl ended up missing the
/// unconditional Focus() call that TextureViewport already had (#638 follow-up).
/// </summary>
internal static class PointerGestures
{
    /// <summary>
    /// True when a pointer press should start a pan gesture: middle-click, or Alt+left-click.
    /// </summary>
    public static bool IsPanGesture(bool isMiddleButtonPressed, bool isLeftButtonPressed, KeyModifiers modifiers) =>
        isMiddleButtonPressed || (isLeftButtonPressed && modifiers.HasFlag(KeyModifiers.Alt));
}
