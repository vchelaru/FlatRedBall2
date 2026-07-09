using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

// #535 M3: the browser build has no filesystem to write a path to, so Save needs a stream-based
// seam. It also has no filesystem to re-read a PNG header from when converting the in-memory UV
// model back to Pixel for a file that was originally Pixel-format -- unlike the path-based
// SaveAnimationChainList(string), which can re-read from achxDirectory, the stream overload must
// reuse the knownTextureSizes captured at LoadAnimationChain time.
public class ProjectManagerSaveTests
{
    private static (ProjectManager pm, AnimationFrameSave frame) LoadPixelChainWithKnownSizes()
    {
        var pm = new ProjectManager();
        var frame = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0f,
            RightCoordinate = 32f,
            TopCoordinate = 0f,
            BottomCoordinate = 64f,
        };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(frame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        preParsed.AnimationChains.Add(chain);

        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>
        {
            ["sprite.png"] = (32, 64),
        };

        // Never written to disk -- if SaveAnimationChainList(Stream) fell back to reading a PNG
        // header from disk instead of reusing this dictionary, the round trip below would
        // silently stay in UV instead of writing back Pixel coordinates.
        pm.LoadAnimationChain(
            new FilePath(TestPaths.Abs("browser", "does-not-exist.achx")),
            preParsed,
            knownTextureSizes);

        return (pm, frame);
    }

    [Fact]
    public void SaveAnimationChainList_Stream_WritesPixelCoordinates_WhenOnDiskFormatIsPixel()
    {
        var (pm, _) = LoadPixelChainWithKnownSizes();

        using var stream = new MemoryStream();
        pm.SaveAnimationChainList(stream);

        stream.Position = 0;
        var written = AnimationChainListSave.FromString(new StreamReader(stream).ReadToEnd());

        Assert.Equal(TextureCoordinateType.Pixel, written.CoordinateType);
        var writtenFrame = written.AnimationChains.Single().Frames.Single();
        Assert.Equal(0f, writtenFrame.LeftCoordinate);
        Assert.Equal(32f, writtenFrame.RightCoordinate);
        Assert.Equal(0f, writtenFrame.TopCoordinate);
        Assert.Equal(64f, writtenFrame.BottomCoordinate);
    }

    [Fact]
    public void SaveAnimationChainList_Stream_LeavesInMemoryModelAsUv_WhenOnDiskFormatIsPixel()
    {
        var (pm, frame) = LoadPixelChainWithKnownSizes();

        using var stream = new MemoryStream();
        pm.SaveAnimationChainList(stream);

        // The rendering pipeline needs the in-memory model to stay UV regardless of what was
        // just written to disk/stream.
        Assert.Equal(TextureCoordinateType.UV, pm.AnimationChainListSave!.CoordinateType);
        Assert.Equal(0f, frame.LeftCoordinate);
        Assert.Equal(1f, frame.RightCoordinate);
    }

    [Fact]
    public void SaveAnimationChainList_Stream_WritesUvCoordinates_WhenOnDiskFormatIsUv()
    {
        var pm = new ProjectManager();
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        pm.LoadAnimationChain(new FilePath(TestPaths.Abs("browser", "uv.achx")), preParsed);

        using var stream = new MemoryStream();
        pm.SaveAnimationChainList(stream);

        stream.Position = 0;
        var written = AnimationChainListSave.FromString(new StreamReader(stream).ReadToEnd());
        Assert.Equal(TextureCoordinateType.UV, written.CoordinateType);
    }

    // #647 Phase 9: the browser build's IStorageFile.OpenWriteAsync() stream backs the File
    // System Access API, which only supports async writes. Handing that stream to the
    // synchronous SaveAnimationChainList(Stream) crashed the whole WASM runtime from inside
    // XmlWriter.Dispose(). SaveAnimationChainListAsync is the fix: it must never call a
    // synchronous Write on the destination.
    [Fact]
    public async Task SaveAnimationChainListAsync_Stream_WritesPixelCoordinates_WhenOnDiskFormatIsPixel()
    {
        var (pm, _) = LoadPixelChainWithKnownSizes();

        using var stream = new AsyncOnlyWriteStream();
        await pm.SaveAnimationChainListAsync(stream);

        var written = AnimationChainListSave.FromString(Encoding.UTF8.GetString(stream.ToArray()));

        Assert.Equal(TextureCoordinateType.Pixel, written.CoordinateType);
        var writtenFrame = written.AnimationChains.Single().Frames.Single();
        Assert.Equal(0f, writtenFrame.LeftCoordinate);
        Assert.Equal(32f, writtenFrame.RightCoordinate);
        Assert.Equal(0f, writtenFrame.TopCoordinate);
        Assert.Equal(64f, writtenFrame.BottomCoordinate);
    }

    [Fact]
    public async Task SaveAnimationChainListAsync_Stream_LeavesInMemoryModelAsUv_WhenOnDiskFormatIsPixel()
    {
        var (pm, frame) = LoadPixelChainWithKnownSizes();

        using var stream = new AsyncOnlyWriteStream();
        await pm.SaveAnimationChainListAsync(stream);

        Assert.Equal(TextureCoordinateType.UV, pm.AnimationChainListSave!.CoordinateType);
        Assert.Equal(0f, frame.LeftCoordinate);
        Assert.Equal(1f, frame.RightCoordinate);
    }

    [Fact]
    public async Task SaveAnimationChainListAsync_Stream_WritesUvCoordinates_WhenOnDiskFormatIsUv()
    {
        var pm = new ProjectManager();
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        pm.LoadAnimationChain(new FilePath(TestPaths.Abs("browser", "uv.achx")), preParsed);

        using var stream = new AsyncOnlyWriteStream();
        await pm.SaveAnimationChainListAsync(stream);

        var written = AnimationChainListSave.FromString(Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(TextureCoordinateType.UV, written.CoordinateType);
    }

    /// <summary>
    /// Minimal reproduction of Avalonia.Browser.Storage.WriteableStream's contract: synchronous
    /// Write throws, only WriteAsync is supported. Derives from <see cref="Stream"/> directly and
    /// buffers into a private sink so it can't accidentally round-trip through the overridden
    /// synchronous Write.
    /// </summary>
    private sealed class AsyncOnlyWriteStream : Stream
    {
        private readonly MemoryStream _sink = new();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _sink.Length;
        public override long Position
        {
            get => _sink.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("Browser supports only WriteAsync");

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _sink.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public byte[] ToArray() => _sink.ToArray();
    }
}
