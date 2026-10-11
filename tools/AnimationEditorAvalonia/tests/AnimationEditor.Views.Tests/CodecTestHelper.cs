using SkiaSharp;
using Xunit;

namespace AnimationEditor.Views.Tests;

/// <summary>Decodes animated image bytes with SkiaSharp's own codec, an independent check of the encoders.</summary>
internal static class CodecTestHelper
{
    public static SKCodec Open(byte[] image) => SKCodec.Create(new SKMemoryStream(image))!;

    /// <summary>Writes a one-row PNG whose pixels are given left to right and returns its path.</summary>
    public static string WriteRow(string folder, string name, params SKColor[] pixels)
    {
        using var bitmap = new SKBitmap(pixels.Length, 1);
        for (int x = 0; x < pixels.Length; x++) bitmap.SetPixel(x, 0, pixels[x]);
        string path = Path.Combine(folder, name);
        File.WriteAllBytes(path, bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        return path;
    }

    public static SKColor PixelAt(byte[] image, int frameIndex, int x, int y)
    {
        using var codec = Open(image);
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        int required = codec.FrameInfo[frameIndex].RequiredFrame;
        if (required >= 0)
        {
            Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(required)));
            Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(frameIndex, required)));
        }
        else
        {
            Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, bitmap.GetPixels(), new SKCodecOptions(frameIndex)));
        }
        return bitmap.GetPixel(x, y);
    }
}
