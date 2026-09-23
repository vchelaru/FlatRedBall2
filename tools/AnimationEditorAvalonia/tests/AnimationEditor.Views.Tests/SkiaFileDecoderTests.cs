using AnimationEditor.Views.Services;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.Views.Tests;

public class SkiaFileDecoderTests
{
    [Fact]
    public void DecodeFile_PathLongerThan260Characters_DecodesTheImage()
    {
        // SKBitmap.Decode(string) opens the file natively and fails past the classic Windows
        // MAX_PATH; reading the bytes with .NET first has no such limit.
        string root = Path.Combine(Path.GetTempPath(), "AnimationEditorViewsTests", Guid.NewGuid().ToString("N"));
        string deep = root;
        while (deep.Length < 300)
        {
            deep = Path.Combine(deep, "level-" + new string('x', 20));
        }
        Directory.CreateDirectory(deep);
        string path = Path.Combine(deep, "sheet.png");
        try
        {
            using (SKBitmap bitmap = new SKBitmap(48, 32))
            using (SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
            {
                File.WriteAllBytes(path, data.ToArray());
            }

            using SKBitmap? decoded = SkiaFileDecoder.DecodeFile(path);

            Assert.NotNull(decoded);
            Assert.Equal((48, 32), (decoded!.Width, decoded.Height));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DecodeFile_ZeroByteFile_ReturnsNull()
    {
        // SKBitmap.Decode(byte[]) throws for bytes no codec accepts; the path overload returned
        // null, and callers rely on null (a truncated or locked file must not crash the editor).
        string path = Path.Combine(Path.GetTempPath(), "AnimationEditorViewsTests", Guid.NewGuid().ToString("N") + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Array.Empty<byte>());
        try
        {
            Assert.Null(SkiaFileDecoder.DecodeFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DecodeFile_NotAnImage_ReturnsNull()
    {
        string path = Path.Combine(Path.GetTempPath(), "AnimationEditorViewsTests", Guid.NewGuid().ToString("N") + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "not a png");
        try
        {
            Assert.Null(SkiaFileDecoder.DecodeFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DecodeFile_MissingFile_ReturnsNull()
    {
        string path = Path.Combine(Path.GetTempPath(), "AnimationEditorViewsTests", Guid.NewGuid().ToString("N") + ".png");

        Assert.Null(SkiaFileDecoder.DecodeFile(path));
    }
}
