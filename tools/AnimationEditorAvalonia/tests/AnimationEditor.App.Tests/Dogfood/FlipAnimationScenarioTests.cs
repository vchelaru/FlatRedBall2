using AnimationEditor.App.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The preview plays a short squash/stretch when a frame is flipped, so the user can see which way
/// it turned. The stored flags change at once; only the preview's drawing lags behind.
/// </summary>
public class FlipAnimationScenarioTests
{
    // 16x16 sheet with only the left half opaque, so a horizontal flip visibly moves the art.
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave Frame)> OpenLeftHalfFrameAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePngWithBlob("sheet.png", 16, 16, (0, 0, 8, 16));
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        return (editor, walk.Frames[0]);
    }

    private static bool HasArtAt(PreviewControl preview, SKBitmap bitmap, float dxInSpritePixels)
    {
        // The sprite is centred in the area to the right of and below the 20 px rulers.
        float cx = (bitmap.Width - 20) / 2f + 20 + preview.PanOffset.X;
        float cy = (bitmap.Height - 20) / 2f + 20 + preview.PanOffset.Y;
        SKColor px = bitmap.GetPixel((int)(cx + dxInSpritePixels * preview.Zoom), (int)cy);
        return px.Red > 200 && px.Green < 150 && px.Blue < 100;
    }

    [AvaloniaFact]
    public async Task FlipHorizontalToggle_PlaysFromTheOldPoseThroughACollapseToTheNewOne()
    {
        (AnimationEditorHarness editor, AnimationFrameSave frame) = await OpenLeftHalfFrameAsync();
        using (editor)
        {
            PreviewControl preview = editor.Preview;
            preview.SetZoomPercent(400);
            const int Size = 200;

            editor.Click(editor.Control<ToggleButton>("PropFlipH"));

            frame.FlipHorizontal.ShouldBeTrue("the model changes at once");
            preview.IsFlipAnimating.ShouldBeTrue();
            using (SKBitmap start = preview.RenderToBitmap(Size, Size))
            {
                HasArtAt(preview, start, -4).ShouldBeTrue("starts on the old (left) side");
                HasArtAt(preview, start, 4).ShouldBeFalse();
            }

            preview.AdvanceFlipAnimation(FlipTransitionHalf);
            using (SKBitmap middle = preview.RenderToBitmap(Size, Size))
            {
                HasArtAt(preview, middle, -4).ShouldBeFalse("squashed flat halfway");
                HasArtAt(preview, middle, 4).ShouldBeFalse();
            }

            preview.AdvanceFlipAnimation(1.0);
            preview.IsFlipAnimating.ShouldBeFalse();
            using (SKBitmap end = preview.RenderToBitmap(Size, Size))
            {
                HasArtAt(preview, end, 4).ShouldBeTrue("ends on the new (right) side");
                HasArtAt(preview, end, -4).ShouldBeFalse();
            }
        }
    }

    private const double FlipTransitionHalf = AnimationEditor.Core.Rendering.FlipTransition.DurationSeconds / 2;

    [AvaloniaFact]
    public async Task FlipToggle_DoesNotAnimateWhenAnotherFrameIsShown()
    {
        (AnimationEditorHarness editor, AnimationFrameSave frame) = await OpenLeftHalfFrameAsync();
        using (editor)
        {
            AnimationFrameSave other = new AnimationFrameSave { TextureName = "sheet.png" };
            editor.ChainNamed("Walk").Frames.Add(other);

            editor.Services.AppCommands.SetFrameFlip(new[] { other }, flipHorizontal: true, flipVertical: null);

            editor.Preview.IsFlipAnimating.ShouldBeFalse("the previewed frame is not the one that flipped");
            frame.FlipHorizontal.ShouldBeFalse();
        }
    }
}
