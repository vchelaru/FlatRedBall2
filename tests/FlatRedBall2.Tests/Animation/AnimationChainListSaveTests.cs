using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class AnimationChainListSaveTests
{
    private static AnimationChainListSave Parse(string xml)
        => AnimationChainListSave.FromFile("test.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    [Fact]
    public void FromFile_SingleChainWithTwoFrames_PreservesNamesAndFrameCount()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Walk</Name>" +
            "    <Frame><TextureName>walk1.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "    <Frame><TextureName>walk2.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var result = Parse(xml);

        result.AnimationChains.Count.ShouldBe(1);
        result.AnimationChains[0].Name.ShouldBe("Walk");
        result.AnimationChains[0].Frames.Count.ShouldBe(2);
        result.AnimationChains[0].Frames[0].TextureName.ShouldBe("walk1.png");
        result.AnimationChains[0].Frames[1].FrameLength.ShouldBe(0.1f, tolerance: 0.0001f);
    }

    [Fact]
    public void FromFile_FlipHorizontalOmitted_DefaultsToFalse()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>Idle</Name>" +
            "    <Frame><TextureName>idle.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var result = Parse(xml);

        result.AnimationChains[0].Frames[0].FlipHorizontal.ShouldBeFalse();
    }

    [Fact]
    public void FromFile_TimeMeasurementUnitMillisecond_ParsedCorrectly()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <TimeMeasurementUnit>Millisecond</TimeMeasurementUnit>" +
            "</AnimationChainArraySave>";

        var result = Parse(xml);

        result.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
    }

    [Fact]
    public void ToAnimationChainList_CopiesFlipDiagonalToRuntimeFrame()
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Corner" };
        // Empty TextureName so conversion skips texture loading and the null ContentLoader is never used.
        chain.Frames.Add(new AnimationFrameSave { FlipDiagonal = true });
        save.AnimationChains.Add(chain);

        var frame = save.ToAnimationChainList(null!)[0][0];

        frame.FlipDiagonal.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_WritesToStream_WithoutCallingSynchronousWrite()
    {
        // Avalonia's browser IStorageFile.OpenWriteAsync() stream backs the File System Access
        // API, which only supports async writes -- a synchronous Write/WriteByte call throws and
        // takes down the whole WASM runtime because it happens inside XmlWriter.Dispose(). This
        // fake reproduces that contract so SaveAsync is proven never to call the sync path.
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Walk" });
        using var destination = new AsyncOnlyWriteStream();

        await save.SaveAsync(destination);

        destination.ToArray().Length.ShouldBeGreaterThan(0);
        var written = AnimationChainListSave.FromString(Encoding.UTF8.GetString(destination.ToArray()));
        written.AnimationChains.Single().Name.ShouldBe("Walk");
    }

    [Fact]
    public void Save_ToAsyncOnlyStream_ThrowsInsteadOfSilentlyCorrupting()
    {
        // Documents why SaveAsync exists: handing the synchronous Save(Stream) overload a
        // browser-style async-only stream must fail loudly, not corrupt output.
        var save = new AnimationChainListSave();
        using var destination = new AsyncOnlyWriteStream();

        Should.Throw<InvalidOperationException>(() => save.Save(destination));
    }

    /// <summary>
    /// Minimal reproduction of Avalonia.Browser.Storage.WriteableStream's contract: synchronous
    /// Write throws, only WriteAsync is supported. Derives directly from <see cref="Stream"/>
    /// (not <see cref="MemoryStream"/>) and buffers into a private sink so the async path can't
    /// accidentally round-trip through the overridden synchronous Write via virtual dispatch or
    /// a base-class fast path -- both of which would silently defeat this fake's purpose.
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
