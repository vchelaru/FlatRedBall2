namespace AnimationEditor.Core.Export;

/// <summary>What the user chose in an animated image export dialog.</summary>
/// <param name="FrameRate">Frames per second; one of the format's supported rates.</param>
/// <param name="Transparent">When set, the background is transparent; otherwise it is <paramref name="BackgroundArgb"/>.</param>
/// <param name="BackgroundArgb">Packed <c>0xAARRGGBB</c>; alpha is ignored. Used only when not transparent.</param>
/// <param name="Scale">Whole-number nearest-neighbor upscale, 1 or more.</param>
/// <param name="Lossless">WebP only: store frames without quality loss. GIF ignores it.</param>
/// <param name="Quality">WebP only, 0 to 100: used when not lossless. GIF ignores it.</param>
public sealed record AnimationExportOptions(
    int FrameRate, bool Transparent, uint BackgroundArgb, int Scale, bool Lossless = true, int Quality = 90)
{
    public const int MaxScale = 16;
}
