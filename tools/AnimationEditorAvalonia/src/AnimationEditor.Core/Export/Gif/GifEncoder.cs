using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimationEditor.Core.Export.Gif;

/// <summary>One frame to encode: straight (not premultiplied) RGBA, row-major from the top-left, shown for the delay.</summary>
public sealed record GifRgbaFrame(byte[] Rgba, int DelayCentiseconds);

/// <summary>
/// Writes an animated GIF89a that loops forever. Every frame is the full canvas, drawn against one
/// shared palette. With <c>transparent</c> set, pixels below <see cref="GifPalette.AlphaThreshold"/>
/// become the transparent index and frames restore to background so earlier frames cannot show through.
/// </summary>
public static class GifEncoder
{
    public const int MaxDimension = 65535;

    public static byte[] Encode(int width, int height, IReadOnlyList<GifRgbaFrame> frames, bool transparent)
    {
        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
            throw new ArgumentOutOfRangeException(nameof(width), $"A GIF canvas must be 1 to {MaxDimension} pixels on each side.");
        if (frames.Count == 0)
            throw new ArgumentException("A GIF needs at least one frame.", nameof(frames));
        foreach (var frame in frames)
        {
            if (frame.Rgba.Length != width * height * 4)
                throw new ArgumentException("A frame's pixel buffer does not match the canvas size.", nameof(frames));
        }

        var palette = GifPalette.Build(frames.Select(f => f.Rgba), transparent);
        int tableBits = 1;
        while (1 << tableBits < palette.Count) tableBits++;
        int minCodeSize = Math.Max(2, tableBits);

        using var stream = new MemoryStream();
        Write(stream, "GIF89a"u8);
        WriteShort(stream, width);
        WriteShort(stream, height);
        stream.WriteByte((byte)(0x80 | 0x70 | (tableBits - 1)));
        stream.WriteByte(0); // background color index
        stream.WriteByte(0); // pixel aspect ratio
        Write(stream, palette.PaddedRgb(1 << tableBits));

        // NETSCAPE2.0 application extension: loop forever.
        Write(stream, new byte[] { 0x21, 0xFF, 0x0B });
        Write(stream, "NETSCAPE2.0"u8);
        Write(stream, new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });

        foreach (var frame in frames)
        {
            stream.WriteByte(0x21); // graphic control extension
            stream.WriteByte(0xF9);
            stream.WriteByte(4);
            int disposal = transparent ? 2 : 1;
            stream.WriteByte((byte)(disposal << 2 | (transparent ? 1 : 0)));
            WriteShort(stream, Math.Max(GifTiming.MinDelayCentiseconds, frame.DelayCentiseconds));
            stream.WriteByte((byte)(palette.TransparentIndex ?? 0));
            stream.WriteByte(0);

            stream.WriteByte(0x2C); // image descriptor: full canvas, no local table
            WriteShort(stream, 0);
            WriteShort(stream, 0);
            WriteShort(stream, width);
            WriteShort(stream, height);
            stream.WriteByte(0);

            stream.WriteByte((byte)minCodeSize);
            LzwWriter.Write(stream, palette.ToIndices(frame.Rgba), minCodeSize);
        }

        stream.WriteByte(0x3B);
        return stream.ToArray();
    }

    private static void Write(Stream stream, ReadOnlySpan<byte> bytes) => stream.Write(bytes);

    private static void WriteShort(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)(value >> 8 & 0xFF));
    }

    /// <summary>GIF's variable-width LZW, emitted as 255-byte sub-blocks.</summary>
    private static class LzwWriter
    {
        private const int MaxCodes = 4096;

        public static void Write(Stream stream, byte[] indices, int minCodeSize)
        {
            int alphabet = 1 << minCodeSize;
            int clear = alphabet, end = alphabet + 1;
            // Trie of (prefix code, next pixel) -> code. Codes start above `end`, so 0 means "absent".
            var children = new int[MaxCodes * alphabet];
            var output = new BitSink(stream);

            int codeSize = minCodeSize + 1;
            int next = end + 1;
            output.Write(clear, codeSize);

            int prefix = indices[0];
            for (int i = 1; i < indices.Length; i++)
            {
                int pixel = indices[i];
                int child = children[prefix * alphabet + pixel];
                if (child != 0)
                {
                    prefix = child;
                    continue;
                }

                output.Write(prefix, codeSize);
                if (next < MaxCodes)
                {
                    children[prefix * alphabet + pixel] = next++;
                    // The decoder runs one entry behind, so it widens once the encoder's table passes 2^codeSize.
                    if (next - 1 == 1 << codeSize && codeSize < 12) codeSize++;
                }
                else
                {
                    output.Write(clear, codeSize);
                    Array.Clear(children);
                    codeSize = minCodeSize + 1;
                    next = end + 1;
                }
                prefix = pixel;
            }

            output.Write(prefix, codeSize);
            output.Write(end, codeSize);
            output.Flush();
        }
    }

    private sealed class BitSink
    {
        private readonly Stream _stream;
        private readonly byte[] _block = new byte[255];
        private int _blockLength;
        private int _bits;
        private int _bitCount;

        public BitSink(Stream stream) => _stream = stream;

        public void Write(int code, int size)
        {
            _bits |= code << _bitCount;
            _bitCount += size;
            while (_bitCount >= 8)
            {
                AddByte((byte)(_bits & 0xFF));
                _bits >>= 8;
                _bitCount -= 8;
            }
        }

        public void Flush()
        {
            if (_bitCount > 0) AddByte((byte)(_bits & 0xFF));
            if (_blockLength > 0) FlushBlock();
            _stream.WriteByte(0); // block terminator
        }

        private void AddByte(byte value)
        {
            _block[_blockLength++] = value;
            if (_blockLength == 255) FlushBlock();
        }

        private void FlushBlock()
        {
            _stream.WriteByte((byte)_blockLength);
            _stream.Write(_block, 0, _blockLength);
            _blockLength = 0;
        }
    }
}
