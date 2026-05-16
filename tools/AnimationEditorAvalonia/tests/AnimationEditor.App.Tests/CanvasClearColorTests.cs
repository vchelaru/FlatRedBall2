using AnimationEditor.App.Controls;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that the Skia canvas background color used by both content panels
/// matches the BgCanvas design token (#0e0f12), putting wireframe and preview
/// in the same darkest tier as the animations listbox.
/// </summary>
public class CanvasClearColorTests
{
    private static readonly SKColor BgCanvas = new(0x0e, 0x0f, 0x12);

    [Fact]
    public void WireframeControl_CanvasClearColor_MatchesBgCanvasToken()
    {
        Assert.Equal(BgCanvas, WireframeControl.CanvasClearColor);
    }

    [Fact]
    public void PreviewControl_CanvasClearColor_MatchesBgCanvasToken()
    {
        Assert.Equal(BgCanvas, PreviewControl.CanvasClearColor);
    }
}
