namespace AnimationEditor.Core.Tests;

/// <summary>
/// A deliberately small GIF reader for tests: container blocks and LZW only, so the encoder's
/// output can be checked pixel by pixel without a graphics library.
/// </summary>
internal sealed class GifTestDecoder
{
    public sealed record Frame(int DelayCentiseconds, int Disposal, int? TransparentIndex, byte[] Indices);

    public int Width { get; private set; }
    public int Height { get; private set; }
    public byte[] GlobalPalette { get; private set; } = Array.Empty<byte>();
    public bool HasInfiniteLoopExtension { get; private set; }
    public bool HasTrailer { get; private set; }
    public List<Frame> Frames { get; } = new();

    public static GifTestDecoder Decode(byte[] gif)
    {
        var d = new GifTestDecoder();
        int p = 0;
        string header = System.Text.Encoding.ASCII.GetString(gif, 0, 6);
        if (header != "GIF89a") throw new InvalidDataException("header " + header);
        p = 6;
        d.Width = gif[p] | gif[p + 1] << 8;
        d.Height = gif[p + 2] | gif[p + 3] << 8;
        int packed = gif[p + 4];
        p += 7;
        if ((packed & 0x80) != 0)
        {
            int size = 3 * (1 << ((packed & 7) + 1));
            d.GlobalPalette = gif[p..(p + size)];
            p += size;
        }

        int delay = 0, disposal = 0;
        int? transparent = null;
        while (true)
        {
            byte block = gif[p++];
            if (block == 0x3B) { d.HasTrailer = true; break; }
            if (block == 0x21)
            {
                byte label = gif[p++];
                if (label == 0xF9)
                {
                    int gce = gif[p + 1];
                    disposal = (gce >> 2) & 7;
                    delay = gif[p + 2] | gif[p + 3] << 8;
                    transparent = (gce & 1) != 0 ? gif[p + 4] : null;
                    p += 6;
                    continue;
                }
                if (label == 0xFF)
                {
                    int len = gif[p++];
                    string id = System.Text.Encoding.ASCII.GetString(gif, p, len);
                    p += len;
                    if (id == "NETSCAPE2.0" && gif[p] == 3 && gif[p + 1] == 1 && gif[p + 2] == 0 && gif[p + 3] == 0)
                        d.HasInfiniteLoopExtension = true;
                }
                while (gif[p] != 0) p += gif[p] + 1;
                p++;
                continue;
            }
            if (block != 0x2C) throw new InvalidDataException("unexpected block " + block);

            int w = gif[p + 4] | gif[p + 5] << 8;
            int h = gif[p + 6] | gif[p + 7] << 8;
            p += 9;
            int minCode = gif[p++];
            var data = new List<byte>();
            while (gif[p] != 0)
            {
                int len = gif[p++];
                for (int i = 0; i < len; i++) data.Add(gif[p++]);
            }
            p++;
            d.Frames.Add(new Frame(delay, disposal, transparent, Lzw(data.ToArray(), minCode, w * h)));
            delay = 0; disposal = 0; transparent = null;
        }
        return d;
    }

    private static byte[] Lzw(byte[] data, int minCode, int pixelCount)
    {
        int clear = 1 << minCode, eoi = clear + 1;
        var prefix = new int[4096];
        var suffix = new byte[4096];
        var stack = new byte[4097];
        var output = new byte[pixelCount];
        int outPos = 0;

        int codeSize = minCode + 1, next = eoi + 1, old = -1;
        byte first = 0;
        int bits = 0, bitCount = 0, dataPos = 0;

        while (outPos < pixelCount)
        {
            while (bitCount < codeSize && dataPos < data.Length)
            {
                bits |= data[dataPos++] << bitCount;
                bitCount += 8;
            }
            if (bitCount < codeSize) break;
            int code = bits & ((1 << codeSize) - 1);
            bits >>= codeSize;
            bitCount -= codeSize;

            if (code == clear) { codeSize = minCode + 1; next = eoi + 1; old = -1; continue; }
            if (code == eoi) break;

            if (old == -1)
            {
                output[outPos++] = (byte)code;
                old = code;
                first = (byte)code;
                continue;
            }

            int incoming = code;
            int top = 0;
            if (code >= next)
            {
                stack[top++] = first;
                code = old;
            }
            while (code >= clear)
            {
                stack[top++] = suffix[code];
                code = prefix[code];
            }
            first = (byte)code;
            stack[top++] = first;

            if (next < 4096)
            {
                prefix[next] = old;
                suffix[next] = first;
                next++;
                if (next == 1 << codeSize && codeSize < 12) codeSize++;
            }
            old = incoming;

            while (top > 0 && outPos < pixelCount) output[outPos++] = stack[--top];
        }
        if (outPos != pixelCount) throw new InvalidDataException($"decoded {outPos} of {pixelCount} pixels");
        return output;
    }
}
