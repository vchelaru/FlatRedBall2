using SkiaSharp;

namespace AnimationEditor.App.Theming;

/// <summary>
/// The neutral background and chrome colors the Skia-drawn editor canvases
/// (<c>WireframeControl</c>, <c>PreviewControl</c>) use for a given theme variant.
/// <para>
/// Colors that would otherwise vanish against the opposite background live here
/// (canvas fill, grid lines, texture outline, ruler strips/ticks/labels, and the
/// preview guide lines — bright cyan on dark, a deep blue on light). Truly saturated
/// <i>semantic</i> overlays — frame-region blue, origin/shape gold and green —
/// read legibly on both themes and stay as constants in the controls.
/// </para>
/// <para>
/// <see cref="GuideLine"/> is stored opaque; callers apply their own alpha via
/// <c>SKColor.WithAlpha</c> so the line / label / ruler-tick translucency is preserved.
/// </para>
/// </summary>
internal readonly record struct CanvasPalette(
    SKColor Background,
    SKColor TextureOutline,
    SKColor GridLine,
    SKColor RulerBackground,
    SKColor RulerTick,
    SKColor RulerLabel,
    SKColor RulerBorder,
    SKColor GuideLine)
{
    /// <summary>Dark theme — matches the historical hardcoded values (BgCanvas #0e0f12).</summary>
    public static readonly CanvasPalette Dark = new(
        Background:      new SKColor(0x0e, 0x0f, 0x12),
        TextureOutline:  new SKColor(255, 255, 255, 160),
        GridLine:        new SKColor(255, 255, 255, 35),
        RulerBackground: new SKColor(50, 50, 55),
        RulerTick:       new SKColor(160, 160, 165),
        RulerLabel:      new SKColor(190, 190, 195),
        RulerBorder:     new SKColor(80, 80, 85),
        GuideLine:       new SKColor(0, 200, 255));

    /// <summary>Light theme — dark chrome on a light canvas so the same elements stay visible.</summary>
    public static readonly CanvasPalette Light = new(
        Background:      new SKColor(0xe8, 0xea, 0xed),
        TextureOutline:  new SKColor(0, 0, 0, 140),
        GridLine:        new SKColor(0, 0, 0, 30),
        RulerBackground: new SKColor(225, 227, 231),
        RulerTick:       new SKColor(110, 116, 124),
        RulerLabel:      new SKColor(80, 86, 94),
        RulerBorder:     new SKColor(190, 194, 200),
        GuideLine:       new SKColor(0, 105, 180));

    public static CanvasPalette For(bool isDark) => isDark ? Dark : Light;

    /// <summary>
    /// Returns a copy with <see cref="Background"/> replaced by <paramref name="background"/>.
    /// Chrome colors (grid, rulers, outline, guides) are intentionally left untouched — they
    /// stay theme-driven, so a user-chosen background that is very close to the theme's chrome
    /// can reduce their contrast. See the canvas-background feature caveat.
    /// </summary>
    public CanvasPalette WithBackground(SKColor background) => this with { Background = background };

    /// <summary>
    /// Resolves the palette for a theme variant, applying an optional user background override
    /// (packed <c>0xAARRGGBB</c>). <c>null</c> keeps the theme background; a value replaces only
    /// the background via <see cref="WithBackground"/>, leaving chrome theme-driven.
    /// </summary>
    public static CanvasPalette Resolve(bool isDark, uint? overrideArgb)
    {
        var palette = For(isDark);
        return overrideArgb is uint argb ? palette.WithBackground(new SKColor(argb)) : palette;
    }
}
