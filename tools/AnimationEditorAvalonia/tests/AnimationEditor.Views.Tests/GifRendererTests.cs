using AnimationEditor.Core.Export;
using AnimationEditor.Core.Export.Gif;
using AnimationEditor.Views.Export;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;
using static AnimationEditor.Views.Tests.CodecTestHelper;

namespace AnimationEditor.Views.Tests;

public class GifRendererTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "AnimationEditorViewsTests", Guid.NewGuid().ToString("N"));

    public GifRendererTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static AnimationFrameSave Frame(string texture, float left, float right, float length = 0.1f, float x = 0, float y = 0) =>
        new()
        {
            TextureName = texture, LeftCoordinate = left, RightCoordinate = right,
            TopCoordinate = 0, BottomCoordinate = 1, FrameLength = length, RelativeX = x, RelativeY = y,
        };

    private static AnimationExportOptions Solid(int scale = 1, int fps = 50) =>
        new(fps, Transparent: false, BackgroundArgb: 0xFFFFFFFF, scale);

    private byte[] Render(IReadOnlyList<ExportSourceChain> sources, AnimationExportOptions options) =>
        GifRenderer.Render(sources, frame => Path.Combine(_folder, frame.TextureName), offsetMultiplier: 1f, options);

    [Fact]
    public void Render_TwoFrames_ScalesTheSpriteAndKeepsEachFramesDuration()
    {
        string sheet = WriteRow(_folder, "sheet.png", SKColors.Red, SKColors.Blue);
        var chain = new AnimationChainSave { Name = "Blink", Loop = true };
        chain.Frames.Add(Frame("sheet.png", 0f, 0.5f));
        chain.Frames.Add(Frame("sheet.png", 0.5f, 1f));

        byte[] gif = Render(new[] { new ExportSourceChain(chain) }, Solid(scale: 3));

        using var codec = Open(gif);
        Assert.Equal((3, 3), (codec.Info.Width, codec.Info.Height));
        Assert.Equal(2, codec.FrameCount);
        Assert.Equal(new[] { 100, 100 }, codec.FrameInfo.Select(f => f.Duration).ToArray());
        Assert.Equal(SKColors.Red, PixelAt(gif, 0, 1, 1));
        Assert.Equal(SKColors.Blue, PixelAt(gif, 1, 1, 1));
        Assert.True(File.Exists(sheet));
    }

    [Fact]
    public void Render_FrameOffsets_WidenTheCanvasToHoldEveryPosition()
    {
        WriteRow(_folder, "sheet.png", SKColors.Red);
        var chain = new AnimationChainSave { Name = "Slide", Loop = true };
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 0));
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 4));

        byte[] gif = Render(new[] { new ExportSourceChain(chain) }, Solid());

        using var codec = Open(gif);
        // A 1px sprite centered at x=0 spans [-0.5, 0.5] snapped to [0, 1]; at x=4 it spans [4, 5].
        Assert.Equal(5, codec.Info.Width);
        Assert.Equal(SKColors.Red, PixelAt(gif, 0, 0, 0));
        Assert.Equal(SKColors.White, PixelAt(gif, 0, 4, 0));
        Assert.Equal(SKColors.Red, PixelAt(gif, 1, 4, 0));
    }

    [Fact]
    public void Render_Transparent_LeavesTheBackgroundTransparent()
    {
        WriteRow(_folder, "sheet.png", SKColors.Red);
        var chain = new AnimationChainSave { Name = "Slide", Loop = true };
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 0));
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 2));

        byte[] gif = Render(new[] { new ExportSourceChain(chain) }, new AnimationExportOptions(50, Transparent: true, 0, 1));

        Assert.Equal(SKColors.Red, PixelAt(gif, 0, 0, 0));
        Assert.Equal(0, PixelAt(gif, 0, 2, 0).Alpha);
        Assert.Equal(0, PixelAt(gif, 1, 0, 0).Alpha);
        Assert.Equal(SKColors.Red, PixelAt(gif, 1, 2, 0));
    }

    [Fact]
    public void Render_SeveralAnimations_StackInSelectionOrderWithTheLastOnTop()
    {
        WriteRow(_folder, "back.png", SKColors.Red);
        WriteRow(_folder, "front.png", SKColors.Blue);
        var back = new AnimationChainSave { Name = "Back", Loop = true };
        back.Frames.Add(Frame("back.png", 0f, 1f));
        var front = new AnimationChainSave { Name = "Front", Loop = true };
        front.Frames.Add(Frame("front.png", 0f, 1f));

        byte[] gif = Render(new[] { new ExportSourceChain(back), new ExportSourceChain(front) }, Solid());

        Assert.Equal(SKColors.Blue, PixelAt(gif, 0, 0, 0));
    }

    [Fact]
    public void Render_MoreThanOneFrameRate_ChangesOnlyTheStepsNotTheTotalDuration()
    {
        WriteRow(_folder, "sheet.png", SKColors.Red);
        var chain = new AnimationChainSave { Name = "Slide", Loop = false };
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 0));
        chain.Frames.Add(Frame("sheet.png", 0f, 1f, x: 10));
        chain.Frames[0].OffsetInterpolation = "Linear";

        byte[] at50 = Render(new[] { new ExportSourceChain(chain) }, Solid(fps: 50));
        byte[] at25 = Render(new[] { new ExportSourceChain(chain) }, Solid(fps: 25));

        using var fast = Open(at50);
        using var slow = Open(at25);
        Assert.Equal(200, fast.FrameInfo.Sum(f => f.Duration));
        Assert.Equal(200, slow.FrameInfo.Sum(f => f.Duration));
        Assert.True(fast.FrameCount > slow.FrameCount);
    }

    [Fact]
    public void Render_MissingTexture_ThrowsInsteadOfWritingAnEmptyGif()
    {
        var chain = new AnimationChainSave { Name = "Gone", Loop = true };
        chain.Frames.Add(Frame("missing.png", 0f, 1f));

        Assert.Throws<InvalidOperationException>(() =>
            GifRenderer.Render(new[] { new ExportSourceChain(chain) }, _ => null, 1f, Solid()));
    }
}
