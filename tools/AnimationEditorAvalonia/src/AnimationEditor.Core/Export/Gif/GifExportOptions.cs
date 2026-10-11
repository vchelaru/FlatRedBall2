namespace AnimationEditor.Core.Export.Gif;

/// <summary>What the user chose in the Export as GIF dialog.</summary>
/// <param name="FrameRate">Frames per second; one of <see cref="GifTiming.SupportedFrameRates"/>.</param>
/// <param name="Transparent">When set, the background is transparent (1-bit); otherwise it is <paramref name="BackgroundArgb"/>.</param>
/// <param name="BackgroundArgb">Packed <c>0xAARRGGBB</c>; alpha is ignored. Used only when not transparent.</param>
/// <param name="Scale">Whole-number nearest-neighbor upscale, 1 or more.</param>
public sealed record GifExportOptions(int FrameRate, bool Transparent, uint BackgroundArgb, int Scale)
{
    public const int MaxScale = 16;
}
