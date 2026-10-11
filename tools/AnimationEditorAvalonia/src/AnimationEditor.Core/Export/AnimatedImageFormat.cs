using System.Collections.Generic;
using AnimationEditor.Core.Export.Gif;
using AnimationEditor.Core.Export.Webp;

namespace AnimationEditor.Core.Export;

/// <summary>An animated image export target: what its dialog and save picker need. The encoder lives in the Views assembly.</summary>
public sealed class AnimatedImageFormat
{
    private AnimatedImageFormat(
        string name, string extension, string fileTypeDescription, IReadOnlyList<int> frameRates,
        string? frameRateHint, string transparentHint, bool offersQuality)
    {
        Name = name;
        Extension = extension;
        FileTypeDescription = fileTypeDescription;
        FrameRates = frameRates;
        FrameRateHint = frameRateHint;
        TransparentHint = transparentHint;
        OffersQuality = offersQuality;
    }

    /// <summary>Display name, as in the menu and dialog title.</summary>
    public string Name { get; }

    /// <summary>File extension without the dot.</summary>
    public string Extension { get; }

    public string FileTypeDescription { get; }

    /// <summary>The frame rates the dialog offers, fastest first; the first is the default.</summary>
    public IReadOnlyList<int> FrameRates { get; }

    public string? FrameRateHint { get; }

    public string TransparentHint { get; }

    /// <summary>Whether the dialog shows the lossless and quality choices.</summary>
    public bool OffersQuality { get; }

    public static AnimatedImageFormat Gif { get; } = new(
        "GIF", "gif", "GIF image (*.gif)", GifTiming.SupportedFrameRates,
        frameRateHint: "GIF can't play faster than 50 fps, so 60 isn't offered.",
        transparentHint: "Each pixel is fully on or off. Soft edges get a jagged fringe; fine for pixel art.",
        offersQuality: false);

    public static AnimatedImageFormat Webp { get; } = new(
        "WebP", "webp", "WebP image (*.webp)", WebpTiming.SupportedFrameRates,
        frameRateHint: null,
        transparentHint: "Keeps soft edges, with full alpha.",
        offersQuality: true);
}
