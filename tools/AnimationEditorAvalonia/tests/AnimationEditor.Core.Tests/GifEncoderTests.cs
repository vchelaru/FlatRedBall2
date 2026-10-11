using AnimationEditor.Core.Export.Gif;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GifEncoderTests
{
    private static byte[] Solid(int width, int height, byte r, byte g, byte b, byte a = 255)
    {
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b; rgba[i * 4 + 3] = a;
        }
        return rgba;
    }

    [Fact]
    public void Encode_TwoFrames_WritesTheContainerWithDelaysAndAnInfiniteLoop()
    {
        var frames = new[]
        {
            new GifRgbaFrame(Solid(3, 2, 255, 0, 0), DelayCentiseconds: 7),
            new GifRgbaFrame(Solid(3, 2, 0, 0, 255), DelayCentiseconds: 12),
        };

        var gif = GifTestDecoder.Decode(GifEncoder.Encode(3, 2, frames, transparent: false));

        gif.Width.ShouldBe(3);
        gif.Height.ShouldBe(2);
        gif.HasInfiniteLoopExtension.ShouldBeTrue();
        gif.HasTrailer.ShouldBeTrue();
        gif.Frames.Select(f => f.DelayCentiseconds).ShouldBe(new[] { 7, 12 });
    }

    [Fact]
    public void Encode_Pixels_RoundTripThroughTheGlobalPalette()
    {
        // Left pixel red, right pixel blue.
        var rgba = new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 };

        var gif = GifTestDecoder.Decode(GifEncoder.Encode(2, 1, new[] { new GifRgbaFrame(rgba, 5) }, transparent: false));

        byte[] indices = gif.Frames[0].Indices;
        ColorAt(gif, indices[0]).ShouldBe((255, 0, 0));
        ColorAt(gif, indices[1]).ShouldBe((0, 0, 255));
    }

    [Fact]
    public void Encode_Transparent_FlagsTheIndexAndRestoresToBackgroundBetweenFrames()
    {
        // Left pixel transparent, right pixel opaque green.
        var rgba = new byte[] { 0, 0, 0, 0, 0, 255, 0, 255 };

        var gif = GifTestDecoder.Decode(GifEncoder.Encode(2, 1, new[] { new GifRgbaFrame(rgba, 5) }, transparent: true));

        var frame = gif.Frames[0];
        frame.TransparentIndex.ShouldNotBeNull();
        frame.Indices[0].ShouldBe((byte)frame.TransparentIndex!.Value);
        ColorAt(gif, frame.Indices[1]).ShouldBe((0, 255, 0));
        // Disposal 2: otherwise the previous frame would show through this frame's transparent pixels.
        frame.Disposal.ShouldBe(2);
    }

    [Fact]
    public void Encode_NoisyImage_SurvivesLzwTableResetsAndCodeWidthGrowth()
    {
        // 200x200 pseudo-random pixels over 200 colors overflow the 4096-entry table many times over.
        const int size = 200;
        var random = new Random(1389);
        var rgba = new byte[size * size * 4];
        for (int i = 0; i < size * size; i++)
        {
            int c = random.Next(200);
            rgba[i * 4] = (byte)c; rgba[i * 4 + 1] = (byte)(c * 3); rgba[i * 4 + 2] = (byte)(255 - c); rgba[i * 4 + 3] = 255;
        }

        var gif = GifTestDecoder.Decode(GifEncoder.Encode(size, size, new[] { new GifRgbaFrame(rgba, 2) }, transparent: false));

        byte[] indices = gif.Frames[0].Indices;
        for (int i = 0; i < size * size; i++)
        {
            ColorAt(gif, indices[i]).ShouldBe((rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]));
        }
    }

    [Fact]
    public void Encode_FrameBufferOfTheWrongSize_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            GifEncoder.Encode(2, 2, new[] { new GifRgbaFrame(new byte[4], 2) }, transparent: false));
    }

    private static (int, int, int) ColorAt(GifTestDecoder gif, int index) =>
        (gif.GlobalPalette[index * 3], gif.GlobalPalette[index * 3 + 1], gif.GlobalPalette[index * 3 + 2]);
}
