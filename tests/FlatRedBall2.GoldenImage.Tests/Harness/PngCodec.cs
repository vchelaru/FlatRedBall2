using System.IO;
using Microsoft.Xna.Framework;
using StbRead = StbImageSharp;
using StbWrite = StbImageWriteSharp;

namespace FlatRedBall2.GoldenImage;

/// <summary>
/// PNG encode/decode with no <c>GraphicsDevice</c>. <c>Texture2D.SaveAsPng</c> needs a device,
/// which would make the harness's own tests skip on the machines that most need them.
/// </summary>
public static class PngCodec
{
    public static byte[] Encode(PixelImage image)
    {
        var rgba = new byte[image.Pixels.Length * 4];
        for (int i = 0; i < image.Pixels.Length; i++)
        {
            var c = image.Pixels[i];
            rgba[i * 4] = c.R;
            rgba[i * 4 + 1] = c.G;
            rgba[i * 4 + 2] = c.B;
            rgba[i * 4 + 3] = c.A;
        }

        using var stream = new MemoryStream();
        new StbWrite.ImageWriter().WritePng(
            rgba, image.Width, image.Height, StbWrite.ColorComponents.RedGreenBlueAlpha, stream);
        return stream.ToArray();
    }

    public static PixelImage Decode(byte[] png)
    {
        var decoded = StbRead.ImageResult.FromMemory(png, StbRead.ColorComponents.RedGreenBlueAlpha);
        var pixels = new Color[decoded.Width * decoded.Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(
                decoded.Data[i * 4], decoded.Data[i * 4 + 1], decoded.Data[i * 4 + 2], decoded.Data[i * 4 + 3]);
        }
        return new PixelImage(decoded.Width, decoded.Height, pixels);
    }

    public static PixelImage Load(string path) => Decode(File.ReadAllBytes(path));

    public static void Save(PixelImage image, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, Encode(image));
    }
}
