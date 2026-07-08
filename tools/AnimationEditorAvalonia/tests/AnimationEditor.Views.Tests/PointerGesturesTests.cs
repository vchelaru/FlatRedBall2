using AnimationEditor.App.Controls;
using Avalonia.Input;
using Xunit;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// TextureViewport (wireframe + PNG diff pane) and PreviewControl each had their own copy of the
/// "is this a pan gesture" check. The copies stayed textually identical, but PreviewControl's
/// pointer-press handling separately forgot the accompanying Focus() call that TextureViewport
/// had (#638 follow-up) -- a sign the duplication itself was the risk, not just that one bug.
/// PointerGestures.IsPanGesture is the single source of truth both controls now call.
/// </summary>
public class PointerGesturesTests
{
    [Fact]
    public void MiddleButtonPressed_IsPanGesture_RegardlessOfModifiers()
    {
        Assert.True(PointerGestures.IsPanGesture(
            isMiddleButtonPressed: true, isLeftButtonPressed: false, modifiers: KeyModifiers.None));
    }

    [Fact]
    public void LeftButtonPressed_WithAlt_IsPanGesture()
    {
        Assert.True(PointerGestures.IsPanGesture(
            isMiddleButtonPressed: false, isLeftButtonPressed: true, modifiers: KeyModifiers.Alt));
    }

    [Fact]
    public void LeftButtonPressed_WithoutAlt_IsNotPanGesture()
    {
        Assert.False(PointerGestures.IsPanGesture(
            isMiddleButtonPressed: false, isLeftButtonPressed: true, modifiers: KeyModifiers.None));
    }

    [Fact]
    public void RightButtonPressed_IsNotPanGesture()
    {
        Assert.False(PointerGestures.IsPanGesture(
            isMiddleButtonPressed: false, isLeftButtonPressed: false, modifiers: KeyModifiers.None));
    }
}
