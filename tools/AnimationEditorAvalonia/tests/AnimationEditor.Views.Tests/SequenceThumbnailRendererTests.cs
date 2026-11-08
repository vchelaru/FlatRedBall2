using AnimationEditor.App.Services;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// The one renderer behind the project-tree .seqj rows and the sequence list rows.
public class SequenceThumbnailRendererTests
{
    private static SKBitmap Solid(int size, SKColor color)
    {
        var bm = new SKBitmap(size, size);
        bm.Erase(color);
        return bm;
    }

    private static SequenceThumbnailLayer Layer(string texture, float offsetX = 0, float offsetY = 0) =>
        new(new AnimationFrameSave { TextureName = texture, RightCoordinate = 1, BottomCoordinate = 1 }, texture, offsetX, offsetY, default);

    private static Task<SKBitmap?> Decode(string path) =>
        Task.FromResult<SKBitmap?>(path == "red" ? Solid(16, SKColors.Red) : path == "blue" ? Solid(16, SKColors.Blue) : null);

    [Fact]
    public async Task TwoSpritesAtTheSameSpot_TheLaterLayerIsInFront()
    {
        using var thumb = await SequenceThumbnailRenderer.RenderAsync([Layer("red"), Layer("blue")], null, Decode, 28, 28);

        Assert.Equal(28, thumb!.Width);
        Assert.Equal(SKColors.Blue, thumb.GetPixel(14, 14).WithAlpha(255));
    }

    [Fact]
    public async Task TwoSpritesSideBySide_AreCenteredAsOneBounds()
    {
        // 32 wide by 16 tall in total, fitted into 28: the left half is red, the right half blue.
        using var thumb = await SequenceThumbnailRenderer.RenderAsync([Layer("red"), Layer("blue", offsetX: 16)], null, Decode, 28, 28);

        Assert.Equal(28, thumb!.Width);
        Assert.Equal(14, thumb.Height);
        Assert.Equal(SKColors.Red, thumb.GetPixel(5, 7).WithAlpha(255));
        Assert.Equal(SKColors.Blue, thumb.GetPixel(22, 7).WithAlpha(255));
    }

    [Fact]
    public async Task ASpriteWhoseTextureCannotBeRead_IsLeftOut_AndNoneLeftIsNull()
    {
        using var onlyRed = await SequenceThumbnailRenderer.RenderAsync([Layer("red"), Layer("missing")], null, Decode, 28, 28);
        var none = await SequenceThumbnailRenderer.RenderAsync([Layer("missing")], null, Decode, 28, 28);

        Assert.Equal(28, onlyRed!.Width);
        Assert.Equal(SKColors.Red, onlyRed.GetPixel(14, 14).WithAlpha(255));
        Assert.Null(none);
    }

    [Fact]
    public async Task OneSprite_IsFittedExactlyLikeAnAchxRowsFirstFrame()
    {
        var layer = Layer("red", offsetX: 40, offsetY: -9);
        using var expected = ThumbnailService.RenderFrameThumbnail(Solid(16, SKColors.Red), layer.Frame, default, 28, 28);

        using var thumb = await SequenceThumbnailRenderer.RenderAsync([layer], null, Decode, 28, 28);

        Assert.Equal(expected!.Width, thumb!.Width);
        Assert.Equal(expected.Height, thumb.Height);
    }
}
