using System;
using System.IO;
using System.Text;

namespace FlatRedBall2.Tests.Content.Aseprite;

/// <summary>
/// Builds minimal valid Aseprite (.ase) binary files for testing.
/// Each frame gets a distinct solid color so duplicate-frame merging won't collapse them.
/// </summary>
internal static class AsepriteTestFixture
{
    /// <summary>
    /// Builds a file with no tags — simulating an Aseprite file where the user
    /// authored frames but never defined any animation tags.
    /// </summary>
    public static byte[] BuildUntagged(int width, int height, int frameCount, int frameDurationMs)
        => BuildInternal(width, height, frameCount, tagName: null, frameDurationMs);

    public static byte[] Build(int width, int height, int frameCount, string tagName, int frameDurationMs)
        => BuildInternal(width, height, frameCount, tagName, frameDurationMs);

    private static byte[] BuildInternal(int width, int height, int frameCount, string? tagName, int frameDurationMs)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // ── File header (128 bytes) ──
        long fileSizePos = ms.Position;
        w.Write(0u);                    // file size (patched later)
        w.Write((ushort)0xA5E0);        // magic
        w.Write((ushort)frameCount);
        w.Write((ushort)width);
        w.Write((ushort)height);
        w.Write((ushort)32);            // color depth: RGBA
        w.Write(1u);                    // flags: layer opacity valid
        w.Write((ushort)frameDurationMs);
        w.Write(0u);                    // reserved
        w.Write(0u);                    // reserved
        w.Write((byte)0);              // transparent color index
        w.Write(new byte[3]);          // padding
        w.Write((ushort)0);            // number of colors (0 = 256 for indexed; irrelevant for RGBA)
        w.Write((byte)1);             // pixel width ratio
        w.Write((byte)1);             // pixel height ratio
        w.Write((short)0);            // grid X
        w.Write((short)0);            // grid Y
        w.Write((ushort)16);           // grid width
        w.Write((ushort)16);           // grid height
        w.Write(new byte[84]);         // future/reserved

        // ── Frame 0: layer chunk + cel chunk + (optional) tags chunk ──
        WriteFrame(w, 0, width, height, frameDurationMs, includeLayer: true, tagName, frameCount);

        // ── Frames 1+: cel chunk only ──
        for (int i = 1; i < frameCount; i++)
            WriteFrame(w, i, width, height, frameDurationMs, includeLayer: false, null, 0);

        // Patch file size
        uint fileSize = (uint)ms.Length;
        ms.Position = fileSizePos;
        w.Write(fileSize);

        return ms.ToArray();
    }

    private static void WriteFrame(BinaryWriter w, int frameIndex, int width, int height,
        int durationMs, bool includeLayer, string? tagName, int totalFrames)
    {
        using var chunkMs = new MemoryStream();
        using var chunkW = new BinaryWriter(chunkMs);
        int chunkCount = 0;

        if (includeLayer)
        {
            WriteLayerChunk(chunkW, "Layer 1");
            chunkCount++;
        }

        WriteCelChunk(chunkW, frameIndex, width, height);
        chunkCount++;

        if (includeLayer && tagName != null)
        {
            WriteTagsChunk(chunkW, tagName, totalFrames);
            chunkCount++;
        }

        byte[] chunkData = chunkMs.ToArray();

        // Frame header (16 bytes)
        w.Write((uint)(16 + chunkData.Length)); // bytes in frame
        w.Write((ushort)0xF1FA);                // magic
        w.Write((ushort)chunkCount);            // old chunk count
        w.Write((ushort)durationMs);            // frame duration
        w.Write(new byte[2]);                   // reserved
        w.Write((uint)chunkCount);              // new chunk count

        w.Write(chunkData);
    }

    private static void WriteLayerChunk(BinaryWriter w, string name)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        int dataSize = 2 + 2 + 2 + 2 + 2 + 2 + 1 + 3 + 2 + nameBytes.Length;

        // Chunk header
        w.Write((uint)(6 + dataSize));  // chunk size (header + data)
        w.Write((ushort)0x2004);        // chunk type: layer

        // Layer data
        w.Write((ushort)1);             // flags: visible
        w.Write((ushort)0);             // type: normal
        w.Write((ushort)0);             // child level
        w.Write((ushort)0);             // default width (ignored)
        w.Write((ushort)0);             // default height (ignored)
        w.Write((ushort)0);             // blend mode: normal
        w.Write((byte)255);            // opacity
        w.Write(new byte[3]);          // future
        w.Write((ushort)nameBytes.Length);
        w.Write(nameBytes);
    }

    private static void WriteCelChunk(BinaryWriter w, int frameIndex, int width, int height)
    {
        int pixelCount = width * height;
        int pixelDataSize = pixelCount * 4; // RGBA
        int dataSize = 2 + 2 + 2 + 1 + 2 + 2 + 5 + 2 + 2 + pixelDataSize;

        // Chunk header
        w.Write((uint)(6 + dataSize));
        w.Write((ushort)0x2005);        // chunk type: cel

        // Cel data
        w.Write((ushort)0);             // layer index
        w.Write((short)0);             // x position
        w.Write((short)0);             // y position
        w.Write((byte)255);            // opacity
        w.Write((ushort)0);            // cel type: raw
        w.Write((short)0);            // z-index
        w.Write(new byte[5]);          // reserved

        // Raw cel data
        w.Write((ushort)width);
        w.Write((ushort)height);

        // Distinct color per frame so SpriteSheetProcessor won't merge duplicates
        byte r = (byte)((frameIndex * 80 + 100) % 256);
        byte g = (byte)((frameIndex * 50 + 50) % 256);
        byte b = (byte)((frameIndex * 30 + 200) % 256);
        for (int i = 0; i < pixelCount; i++)
        {
            w.Write(r);
            w.Write(g);
            w.Write(b);
            w.Write((byte)255); // alpha
        }
    }

    private static void WriteTagsChunk(BinaryWriter w, string tagName, int totalFrames)
    {
        byte[] nameBytes = Encoding.UTF8.GetBytes(tagName);
        int tagDataSize = 2 + 2 + 1 + 2 + 6 + 3 + 1 + 2 + nameBytes.Length;
        int dataSize = 2 + 8 + tagDataSize;

        // Chunk header
        w.Write((uint)(6 + dataSize));
        w.Write((ushort)0x2018);        // chunk type: tags

        // Tags data
        w.Write((ushort)1);             // number of tags
        w.Write(new byte[8]);          // reserved

        // Tag entry
        w.Write((ushort)0);             // from frame
        w.Write((ushort)(totalFrames - 1)); // to frame
        w.Write((byte)0);             // loop direction: forward
        w.Write((ushort)0);            // repeat: 0 = infinite
        w.Write(new byte[6]);          // reserved
        w.Write(new byte[3]);          // deprecated tag color
        w.Write((byte)0);             // extra
        w.Write((ushort)nameBytes.Length);
        w.Write(nameBytes);
    }
}
