using AnimationEditor.App.Theming;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies the neutral background/chrome colors the Skia-drawn editor canvases use
/// per theme variant. The dark background must stay on the BgCanvas design token
/// (#0e0f12); the light variant must be a genuinely light color so frames, grid,
/// and rulers read correctly.
/// </summary>
public class CanvasPaletteTests
{
    [Fact]
    public void For_Dark_BackgroundMatchesBgCanvasToken()
    {
        var expected = new SKColor(0x0e, 0x0f, 0x12);

        Assert.Equal(expected, CanvasPalette.For(isDark: true).Background);
    }

    [Fact]
    public void For_Light_BackgroundIsLight()
    {
        var expected = new SKColor(0xe8, 0xea, 0xed);

        Assert.Equal(expected, CanvasPalette.For(isDark: false).Background);
    }

    [Fact]
    public void For_LightVsDark_GridLineContrastsWithBackground()
    {
        // Dark grid lines are near-white (visible on a dark canvas); light grid lines
        // must be near-black (visible on a light canvas), otherwise the grid vanishes.
        var dark = CanvasPalette.For(isDark: true);
        var light = CanvasPalette.For(isDark: false);

        Assert.True(dark.GridLine.Red > 200, "dark-mode grid line should be near-white");
        Assert.True(light.GridLine.Red < 80, "light-mode grid line should be near-black");
    }

    [Fact]
    public void Resolve_NullOverride_UsesThemeBackground()
    {
        var expected = CanvasPalette.For(isDark: true).Background;

        Assert.Equal(expected, CanvasPalette.Resolve(isDark: true, backgroundOverrideArgb: null).Background);
    }

    [Fact]
    public void Resolve_WithBackgroundOverride_UsesOverrideBackgroundAndKeepsChrome()
    {
        // Opaque steel blue as a packed 0xAARRGGBB value.
        uint argb = 0xFF4080C0;
        var themed = CanvasPalette.For(isDark: true);

        var result = CanvasPalette.Resolve(isDark: true, backgroundOverrideArgb: argb);

        Assert.Equal(new SKColor(argb), result.Background);
        // Chrome stays theme-driven — only the background is overridden.
        Assert.Equal(themed.GridLine, result.GridLine);
        Assert.Equal(themed.GuideLine, result.GuideLine);
    }

    [Fact]
    public void Resolve_NullGuideLineOverride_UsesThemeGuideLine()
    {
        var expected = CanvasPalette.For(isDark: true).GuideLine;

        Assert.Equal(expected, CanvasPalette.Resolve(isDark: true, backgroundOverrideArgb: null, guideLineOverrideArgb: null).GuideLine);
    }

    [Fact]
    public void Resolve_WithGuideLineOverride_UsesOverrideGuideLineAndKeepsBackground()
    {
        // Opaque magenta as a packed 0xAARRGGBB value.
        uint argb = 0xFFFF00FF;
        var themed = CanvasPalette.For(isDark: true);

        var result = CanvasPalette.Resolve(isDark: true, backgroundOverrideArgb: null, guideLineOverrideArgb: argb);

        Assert.Equal(new SKColor(argb), result.GuideLine);
        // Background stays theme-driven — only the guide line is overridden.
        Assert.Equal(themed.Background, result.Background);
    }

    [Fact]
    public void Resolve_WithBothOverrides_AppliesBothIndependently()
    {
        uint bgArgb = 0xFF4080C0;
        uint guideArgb = 0xFFFF00FF;

        var result = CanvasPalette.Resolve(isDark: true, backgroundOverrideArgb: bgArgb, guideLineOverrideArgb: guideArgb);

        Assert.Equal(new SKColor(bgArgb), result.Background);
        Assert.Equal(new SKColor(guideArgb), result.GuideLine);
    }

    [Fact]
    public void WithBackground_ReplacesBackgroundAndKeepsChrome()
    {
        var basePalette = CanvasPalette.For(isDark: true);
        var custom = new SKColor(0x40, 0x80, 0xC0);

        var result = basePalette.WithBackground(custom);

        Assert.Equal(custom, result.Background);
        Assert.Equal(basePalette.GridLine, result.GridLine);
        Assert.Equal(basePalette.RulerBackground, result.RulerBackground);
    }

    [Fact]
    public void For_LightGuideLine_IsDarkerThanDarkGuideLine()
    {
        // The dark-theme guide is bright cyan; on a light canvas that washes out, so the
        // light-theme guide must be a deeper blue with clearly lower luminance.
        var dark = CanvasPalette.For(isDark: true);
        var light = CanvasPalette.For(isDark: false);

        int darkSum  = dark.GuideLine.Red  + dark.GuideLine.Green  + dark.GuideLine.Blue;
        int lightSum = light.GuideLine.Red + light.GuideLine.Green + light.GuideLine.Blue;
        Assert.True(lightSum < darkSum, "light-mode guide should be darker than the dark-mode cyan");
    }
}
