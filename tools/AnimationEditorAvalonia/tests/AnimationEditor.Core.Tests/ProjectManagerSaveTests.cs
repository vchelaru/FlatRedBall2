using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
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
    [Fact]
    public void SaveAnimationChainList_Document_WritesThatDocumentInTheGivenFormat_NotTheCurrentOne()
    {
        var pm = new ProjectManager();
        var current = new AnimationChainListSave();
        current.AnimationChains.Add(new AnimationChainSave { Name = "Current" });
        pm.AnimationChainListSave = current;
        pm.OnDiskCoordinateType = TextureCoordinateType.Pixel;
        var other = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var otherChain = new AnimationChainSave { Name = "Other" };
        otherChain.Frames.Add(new AnimationFrameSave { TextureName = "sprite.png", LeftCoordinate = 0.25f, RightCoordinate = 0.5f });
        other.AnimationChains.Add(otherChain);
        string dir = Path.Combine(Path.GetTempPath(), "AnimationEditorCoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "other.achx");

        pm.SaveAnimationChainList(other, path, TextureCoordinateType.UV);

        var saved = AnimationChainListSave.FromFile(path);
        Assert.Equal("Other", Assert.Single(saved.AnimationChains).Name);
        Assert.Equal(TextureCoordinateType.UV, saved.CoordinateType);
        Assert.Equal(0.25f, saved.AnimationChains[0].Frames[0].LeftCoordinate);
        Assert.Same(current, pm.AnimationChainListSave);
        Assert.Equal(0.25f, otherChain.Frames[0].LeftCoordinate);
    }

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

    // #1135: a frame whose texture size can't be resolved (missing/unbuilt PNG, and absent from
    // knownTextureSizes) must not silently fall through the Pixel conversion loop -- the old
    // behavior left that one frame's coordinates in UV scale while still flipping
    // acls.CoordinateType to Pixel, producing a file whose header lies about that frame's
    // coordinate scale. Saving to Pixel format is a persisted artifact, so this must fail loudly
    // instead, and it must not mutate any frame (including ones whose texture *did* resolve) on
    // the way to failing.
    [Fact]
    public void SaveAnimationChainList_Stream_Throws_WhenPixelSaveCannotResolveATextureSize()
    {
        var pm = new ProjectManager();
        var knownFrame = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0f,
            RightCoordinate = 1f,
            TopCoordinate = 0f,
            BottomCoordinate = 1f,
        };
        var unresolvedFrame = new AnimationFrameSave
        {
            TextureName = "missing.png",
            LeftCoordinate = 0f,
            RightCoordinate = 0.5f,
            TopCoordinate = 0f,
            BottomCoordinate = 0.5f,
        };
        var chain = new AnimationChainSave { Name = "Chain1" };
        chain.Frames.Add(knownFrame);
        chain.Frames.Add(unresolvedFrame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        preParsed.AnimationChains.Add(chain);

        // "missing.png" is intentionally absent from knownTextureSizes, and no file exists on
        // disk for it either (the stream overload has no achxDirectory to read from at all).
        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>
        {
            ["sprite.png"] = (32, 64),
        };

        // Loaded as UV (no conversion needed on load), so the failure surfaces on save, not load.
        pm.LoadAnimationChain(
            new FilePath(TestPaths.Abs("browser", "does-not-exist.achx")), preParsed, knownTextureSizes);
        pm.OnDiskCoordinateType = TextureCoordinateType.Pixel;

        using var stream = new MemoryStream();
        Assert.ThrowsAny<Exception>(() => pm.SaveAnimationChainList(stream));

        // The failed save must not have mutated anything, including the frame that did resolve.
        Assert.Equal(TextureCoordinateType.UV, pm.AnimationChainListSave!.CoordinateType);
        Assert.Equal(0f, knownFrame.LeftCoordinate);
        Assert.Equal(1f, knownFrame.RightCoordinate);
        Assert.Equal(0f, unresolvedFrame.LeftCoordinate);
        Assert.Equal(0.5f, unresolvedFrame.RightCoordinate);
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

    // #937: a frame that never had a <ShapeCollectionSave> element on disk (ShapesSave == null
    // straight out of ParseXml/ParseJson) must round-trip through LoadAnimationChain and back out
    // to Save/SaveJson still null, not a force-populated empty ShapesSave. AnimationChainListSave's
    // own writer already omits the wrapper correctly for a null ShapesSave (see
    // Save_FrameWithNullShapes_OmitsShapeCollectionElement in AchxSerializationTests) -- this test
    // guards the AnimationEditor-side load path, which used to defeat that by unconditionally
    // doing `frame.ShapesSave ??= new ShapesSave()` on every loaded frame.
    [Fact]
    public void SaveAnimationChainList_Stream_OmitsShapeCollection_ForFrameThatNeverHadShapes()
    {
        var pm = new ProjectManager();
        var frame = new AnimationFrameSave { TextureName = "hero.png", ShapesSave = null };
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(frame);
        var preParsed = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        preParsed.AnimationChains.Add(chain);

        pm.LoadAnimationChain(new FilePath(TestPaths.Abs("browser", "noshapes.achx")), preParsed);

        using var stream = new MemoryStream();
        pm.SaveAnimationChainList(stream);

        stream.Position = 0;
        var xml = new StreamReader(stream).ReadToEnd();
        Assert.DoesNotContain("ShapeCollectionSave", xml);
    }

    // #937 follow-up: AppCommands.AddFrame/AddFrameFromPixelBounds create *new* frames with an
    // eagerly-allocated empty ShapesSave, a second source of the same bloat the load-path fix
    // above addressed -- confirmed live by opening the app, slicing a sprite sheet into frames,
    // and saving: every frame still got an empty shapesSave block, since these frames are never
    // loaded from disk at all. AddFrameCommand.Do() calls SaveCurrentAnimationChainList()
    // immediately, so the empty block is baked in on the very first save.
    [Fact]
    public void SaveAnimationChainList_Stream_OmitsShapeCollection_ForFrameCreatedViaAddFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Coin");

        ctx.AppCommands.AddFrame(chain, "items.png");

        // "items.png" isn't a real file on disk -- this test is about the ShapesSave omission,
        // not on-disk pixel-coordinate conversion (#1135), so stay in UV.
        ctx.ProjectManager.OnDiskCoordinateType = TextureCoordinateType.UV;

        using var stream = new MemoryStream();
        ctx.ProjectManager.SaveAnimationChainList(stream);

        stream.Position = 0;
        var xml = new StreamReader(stream).ReadToEnd();
        Assert.DoesNotContain("ShapeCollectionSave", xml);
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
