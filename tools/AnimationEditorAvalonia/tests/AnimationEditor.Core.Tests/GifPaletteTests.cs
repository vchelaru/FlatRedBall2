using AnimationEditor.Core.Export.Gif;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GifPaletteTests
{
    private static byte[] Pixels(params (byte r, byte g, byte b, byte a)[] pixels) =>
        pixels.SelectMany(p => new[] { p.r, p.g, p.b, p.a }).ToArray();

    [Fact]
    public void Build_FewColors_KeepsThemExactly()
    {
        var frame = Pixels((255, 0, 0, 255), (0, 255, 0, 255), (255, 0, 0, 255));

        var palette = GifPalette.Build(new[] { frame }, transparency: false);
        byte[] indices = palette.ToIndices(frame);

        palette.Count.ShouldBe(2);
        indices[0].ShouldBe(indices[2]);
        indices[0].ShouldNotBe(indices[1]);
        palette.ColorAt(indices[0]).ShouldBe((255, 0, 0));
        palette.ColorAt(indices[1]).ShouldBe((0, 255, 0));
    }

    [Fact]
    public void Build_Transparency_SendsLowAlphaPixelsToTheTransparentIndex()
    {
        // Alpha 127 is below the 128 cutoff; alpha 128 stays opaque.
        var frame = Pixels((10, 20, 30, 0), (10, 20, 30, 127), (10, 20, 30, 128), (10, 20, 30, 255));

        var palette = GifPalette.Build(new[] { frame }, transparency: true);
        byte[] indices = palette.ToIndices(frame);

        palette.TransparentIndex.ShouldNotBeNull();
        indices[0].ShouldBe((byte)palette.TransparentIndex!.Value);
        indices[1].ShouldBe((byte)palette.TransparentIndex.Value);
        indices[2].ShouldNotBe((byte)palette.TransparentIndex.Value);
        palette.ColorAt(indices[2]).ShouldBe((10, 20, 30));
        indices[3].ShouldBe(indices[2]);
    }

    [Fact]
    public void Build_MoreThan256Colors_ReducesToAValidPaletteWithNearbyColors()
    {
        // A 32x16 gradient of 512 distinct colors.
        var pixels = new List<(byte, byte, byte, byte)>();
        for (int i = 0; i < 512; i++)
            pixels.Add(((byte)(i / 2), (byte)(255 - i / 2), (byte)(i % 2 * 40), 255));
        byte[] frame = Pixels(pixels.ToArray());

        var palette = GifPalette.Build(new[] { frame }, transparency: false);
        byte[] indices = palette.ToIndices(frame);

        palette.Count.ShouldBeLessThanOrEqualTo(256);
        for (int i = 0; i < 512; i++)
        {
            var (r, g, b) = palette.ColorAt(indices[i]);
            Math.Abs(r - frame[i * 4]).ShouldBeLessThan(16);
            Math.Abs(g - frame[i * 4 + 1]).ShouldBeLessThan(16);
            Math.Abs(b - frame[i * 4 + 2]).ShouldBeLessThan(48);
        }
    }

    [Fact]
    public void Build_SharesOnePaletteAcrossFrames()
    {
        var first = Pixels((1, 2, 3, 255));
        var second = Pixels((4, 5, 6, 255));

        var palette = GifPalette.Build(new[] { first, second }, transparency: false);

        palette.ColorAt(palette.ToIndices(first)[0]).ShouldBe((1, 2, 3));
        palette.ColorAt(palette.ToIndices(second)[0]).ShouldBe((4, 5, 6));
    }
}
