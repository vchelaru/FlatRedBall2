using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Content;

/// <summary>
/// File-loaded textures must be premultiplied on every platform: sprites draw with the default
/// premultiplied <c>BlendState.AlphaBlend</c>, and neither desktop backend's
/// <c>Texture2D.FromStream</c> premultiplies.
/// </summary>
[Collection(GraphicsDeviceCollection.Name)]
public class ContentLoaderPremultiplyTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public ContentLoaderPremultiplyTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    // 2x1 PNG: (255,0,0,128) half-transparent red, then (255,0,0,0) fully transparent red.
    private static readonly byte[] HalfAndFullyTransparent = EncodePng(
        2, 1, new byte[] { 255, 0, 0, 128, 255, 0, 0, 0 });

    private ContentLoader? CreateLoader(byte[] png)
    {
        if (!_fixture.IsAvailable)
            return null;
        var loader = new ContentLoader();
        loader.Initialize(_fixture.Game!.Content, _fixture.GraphicsDevice!);
        loader.StreamProvider = _ => new MemoryStream(png);
        return loader;
    }

    [Fact]
    public void Load_PngWithTranslucentAndTransparentTexels_PremultipliesColor()
    {
        var loader = CreateLoader(HalfAndFullyTransparent);
        if (loader is null)
            return;

        var texture = loader.Load<Texture2D>("Content/test.png");

        var pixels = new Color[2];
        texture.GetData(pixels);
        pixels[0].ShouldBe(new Color(128, 0, 0, 128));
        pixels[1].ShouldBe(new Color(0, 0, 0, 0));
    }

    [Fact]
    public void TryReload_PngWithTranslucentAndTransparentTexels_PremultipliesColor()
    {
        var loader = CreateLoader(HalfAndFullyTransparent);
        if (loader is null)
            return;
        var texture = loader.Load<Texture2D>("Content/test.png");
        texture.SetData(new[] { Color.Lime, Color.Lime });

        loader.TryReload("Content/test.png").ShouldBeTrue();

        var pixels = new Color[2];
        texture.GetData(pixels);
        pixels[0].ShouldBe(new Color(128, 0, 0, 128));
        pixels[1].ShouldBe(new Color(0, 0, 0, 0));
    }

    // Minimal RGBA8 PNG writer so the test controls the exact stored texel values.
    private static byte[] EncodePng(int width, int height, byte[] rgba)
    {
        var raw = new MemoryStream();
        for (int y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter: none
            raw.Write(rgba, y * width * 4, width * 4);
        }

        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(raw.ToArray());

        var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new MemoryStream();
        WriteBigEndian(ihdr, width);
        WriteBigEndian(ihdr, height);
        ihdr.Write(new byte[] { 8, 6, 0, 0, 0 }); // 8-bit, RGBA
        WriteChunk(png, "IHDR", ihdr.ToArray());
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        WriteBigEndian(s, data.Length);
        var body = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
        data.CopyTo(body, 4);
        s.Write(body);
        WriteBigEndian(s, unchecked((int)Crc32(body)));
    }

    private static void WriteBigEndian(Stream s, int value)
    {
        s.WriteByte((byte)(value >> 24));
        s.WriteByte((byte)(value >> 16));
        s.WriteByte((byte)(value >> 8));
        s.WriteByte((byte)value);
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
