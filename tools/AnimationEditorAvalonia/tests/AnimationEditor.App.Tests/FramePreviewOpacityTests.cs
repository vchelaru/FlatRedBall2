using AnimationEditor.App;
using Xunit;

namespace AnimationEditor.App.Tests;

// FramePreviewOpacity maps a frame's per-frame Alpha (0-255, null = opaque) onto the preview's
// drawn-sprite opacity, scaled by the layer alpha (1.0 live frame, 0.4 onion skin).
public class FramePreviewOpacityTests
{
    [Fact]
    public void Resolve_NullAlpha_FullyOpaqueAtFullLayer()
    {
        // Unset alpha must not dim the sprite.
        Assert.Equal(255, FramePreviewOpacity.Resolve(null, 1.0f));
    }

    [Fact]
    public void Resolve_HalfAlpha_HalvesOpacity()
    {
        Assert.Equal(128, FramePreviewOpacity.Resolve(128, 1.0f));
    }

    [Fact]
    public void Resolve_ZeroAlpha_FullyTransparent()
    {
        Assert.Equal(0, FramePreviewOpacity.Resolve(0, 1.0f));
    }

    [Fact]
    public void Resolve_CombinesFrameAlphaWithOnionLayer()
    {
        // Onion skin draws at layer 0.4; a fully opaque frame there is 255 * 0.4 ≈ 102.
        Assert.Equal(102, FramePreviewOpacity.Resolve(255, 0.4f));
    }
}
