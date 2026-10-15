using AnimationEditor.Core.Export;
using AnimationEditor.Views.Export;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;
using static AnimationEditor.Views.Tests.CodecTestHelper;

namespace AnimationEditor.Views.Tests;

public class WebpRendererTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "AnimationEditorViewsTests", Guid.NewGuid().ToString("N"));

    public WebpRendererTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static AnimationFrameSave Frame(string texture, float left, float right, float length = 0.1f, float x = 0) =>
        new()
        {
            TextureName = texture, LeftCoordinate = left, RightCoordinate = right,
            TopCoordinate = 0, BottomCoordinate = 1, FrameLength = length, RelativeX = x,
        };

    private static AnimationExportOptions Solid(int scale = 1, int fps = 60) =>
        new(fps, Transparent: false, BackgroundArgb: 0xFFFFFFFF, scale);

    private byte[] Render(IReadOnlyList<ExportSourceChain> sources, AnimationExportOptions options) =>
        WebpRenderer.Render(sources, frame => Path.Combine(_folder, frame.TextureName), offsetMultiplier: 1f, options);

    [Fact]
    public void Render_TwoFrames_ScalesTheSpriteAndKeepsEachFramesDuration()
    {
        WriteRow(_folder, "sheet.png", SKColors.Red, SKColors.Blue);
        var chain = new AnimationChainSave { Name = "Blink", Loop = true };
        chain.Frames.Add(Frame("sheet.png", 0f, 0.5f));
        chain.Frames.Add(Frame("sheet.png", 0.5f, 1f));

        byte[] webp = Render(new[] { new ExportSourceChain(chain) }, Solid(scale: 3));

        using var codec = Open(webp);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        Assert.Equal((3, 3), (codec.Info.Width, codec.Info.Height));
        Assert.Equal(new[] { 100, 100 }, codec.FrameInfo.Select(f => f.Duration).ToArray());
        Assert.Equal(SKColors.Red, PixelAt(webp, 0, 1, 1));
        Assert.Equal(SKColors.Blue, PixelAt(webp, 1, 1, 1));
        Assert.Equal(-1, codec.RepetitionCount); // loops forever
    }

    [Fact]
    public void Render_Transparent_KeepsPartialAlphaAndAnEmptyBackground()
    {
        // Pixel 0 is half-transparent red; the second frame moves the sprite so the canvas has empty space.
        WriteRow(_folder, "sheet.png", new SKColor(255, 0, 0, 128));
        var chain = new AnimationChainSave { Name = "Slide", Loop = true };
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 0));
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 2));

        byte[] webp = Render(new[] { new ExportSourceChain(chain) }, new AnimationExportOptions(60, Transparent: true, 0, 1));

        Assert.InRange(PixelAt(webp, 0, 0, 0).Alpha, 126, 130);
        Assert.Equal(0, PixelAt(webp, 0, 2, 0).Alpha);
        Assert.Equal(0, PixelAt(webp, 1, 0, 0).Alpha);
        Assert.InRange(PixelAt(webp, 1, 2, 0).Alpha, 126, 130);
    }

    [Fact]
    public void Render_StoredLinearCurveAt60Fps_UsesMillisecondDelaysThatTotalTheDuration()
    {
        WriteRow(_folder, "sheet.png", SKColors.Red);
        var chain = new AnimationChainSave { Name = "Slide", Loop = false };
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 0));
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 12));
        chain.Frames[0].OffsetInterpolation = "Linear";

        byte[] webp = Render(new[] { new ExportSourceChain(chain) }, Solid(fps: 60));

        using var codec = Open(webp);
        Assert.Equal(200, codec.FrameInfo.Sum(f => f.Duration));
        Assert.All(codec.FrameInfo.Take(codec.FrameCount - 1), f => Assert.InRange(f.Duration, 16, 17));
    }

    [Fact]
    public void Render_Lossy_StillProducesADecodableAnimation()
    {
        WriteRow(_folder, "sheet.png", SKColors.Red, SKColors.Blue);
        var chain = new AnimationChainSave { Name = "Blink", Loop = true };
        chain.Frames.Add(Frame("sheet.png", 0f, 0.5f));
        chain.Frames.Add(Frame("sheet.png", 0.5f, 1f));

        byte[] webp = Render(
            new[] { new ExportSourceChain(chain) },
            new AnimationExportOptions(30, Transparent: false, 0xFFFFFFFF, Scale: 8, Lossless: false, Quality: 50));

        using var codec = Open(webp);
        Assert.Equal(2, codec.FrameCount);
        Assert.Equal((8, 8), (codec.Info.Width, codec.Info.Height));
    }

    [Fact]
    public void Render_CanvasWiderThanWebpAllows_ThrowsInsteadOfWritingABrokenFile()
    {
        // A 2000px sprite at 16x is 32000px, past WebP's 16383px limit.
        using (var bitmap = new SKBitmap(2000, 1))
        {
            File.WriteAllBytes(Path.Combine(_folder, "wide.png"), bitmap.Encode(SKEncodedImageFormat.Png, 100).ToArray());
        }
        var chain = new AnimationChainSave { Name = "Wide", Loop = true };
        chain.Frames.Add(Frame("wide.png", 0f, 1f));

        Assert.Throws<InvalidOperationException>(() =>
            Render(new[] { new ExportSourceChain(chain) }, Solid(scale: 16)));
    }

    [Fact]
    public void Render_MissingTexture_ThrowsInsteadOfWritingAnEmptyFile()
    {
        var chain = new AnimationChainSave { Name = "Gone", Loop = true };
        chain.Frames.Add(Frame("missing.png", 0f, 1f));

        Assert.Throws<InvalidOperationException>(() =>
            WebpRenderer.Render(new[] { new ExportSourceChain(chain) }, _ => null, 1f, Solid()));
    }
}
