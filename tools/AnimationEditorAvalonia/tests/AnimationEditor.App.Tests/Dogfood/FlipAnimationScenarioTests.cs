using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Rendering;
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

    /// <summary>
    /// The preview's tick timer credits real elapsed time to a running flip, so any stall on a slow
    /// machine (cold JIT, a busy CI runner) moves the animation before the test looks at it. A clock
    /// that never advances leaves the timer ticking but crediting nothing, so only
    /// <see cref="PreviewControl.AdvanceFlipAnimation"/> moves time.
    /// </summary>
    private static void FreezePreviewClock(PreviewControl preview) =>
        preview.SetPlaybackClock(new TickClock(() => 0, 1));

    private static void AssertPose(PreviewControl preview, string stage, double expectedElapsed, bool expectOldSide, bool expectNewSide)
    {
        const int Size = 200;
        using SKBitmap bitmap = preview.RenderToBitmap(Size, Size);
        bool oldSide = HasArtAt(preview, bitmap, -4);
        bool newSide = HasArtAt(preview, bitmap, 4);
        string state = $"stage '{stage}': IsFlipAnimating={preview.IsFlipAnimating}, " +
            $"elapsed={preview.FlipElapsedSeconds?.ToString("0.####") ?? "none"}s of {FlipTransition.DurationSeconds}s " +
            $"(test advanced {expectedElapsed:0.####}s), art on old side={oldSide}, art on new side={newSide}";
        if (expectedElapsed < FlipTransition.DurationSeconds)
        {
            preview.FlipElapsedSeconds.ShouldNotBeNull(state);
            preview.FlipElapsedSeconds!.Value.ShouldBe(expectedElapsed, 1e-9, "something other than the test advanced the flip clock; " + state);
        }
        oldSide.ShouldBe(expectOldSide, state);
        newSide.ShouldBe(expectNewSide, state);
    }

    [AvaloniaFact]
    public async Task FlipHorizontalToggle_PlaysFromTheOldPoseThroughACollapseToTheNewOne()
    {
        (AnimationEditorHarness editor, AnimationFrameSave frame) = await OpenLeftHalfFrameAsync();
        using (editor)
        {
            PreviewControl preview = editor.Preview;
            FreezePreviewClock(preview);
            preview.SetZoomPercent(400);

            editor.Click(editor.Control<ToggleButton>("PropFlipH"));

            frame.FlipHorizontal.ShouldBeTrue("the model changes at once");
            preview.IsFlipAnimating.ShouldBeTrue();
            AssertPose(preview, "old pose", 0, expectOldSide: true, expectNewSide: false);

            double duration = FlipTransition.DurationSeconds;
            preview.AdvanceFlipAnimation(duration * 0.2);
            AssertPose(preview, "collapsing", duration * 0.2, expectOldSide: true, expectNewSide: false);

            preview.AdvanceFlipAnimation(duration * 0.3);
            AssertPose(preview, "collapsed (halfway, flat on the mirror axis)", duration * 0.5, expectOldSide: false, expectNewSide: false);

            preview.AdvanceFlipAnimation(duration * 0.3);
            AssertPose(preview, "expanding", duration * 0.8, expectOldSide: false, expectNewSide: true);

            preview.AdvanceFlipAnimation(duration);
            preview.IsFlipAnimating.ShouldBeFalse("the transition has run its full length");
            AssertPose(preview, "new pose", duration * 1.8, expectOldSide: false, expectNewSide: true);
        }
    }

    [AvaloniaFact]
    public async Task FlipToggle_DoesNotAnimateWhenAnotherFrameIsShown()
    {
        (AnimationEditorHarness editor, AnimationFrameSave frame) = await OpenLeftHalfFrameAsync();
        using (editor)
        {
            FreezePreviewClock(editor.Preview); // a real tick could finish a flip and hide a wrongly started one
            AnimationFrameSave other = new AnimationFrameSave { TextureName = "sheet.png" };
            editor.ChainNamed("Walk").Frames.Add(other);

            editor.Services.AppCommands.SetFrameFlip(new[] { other }, flipHorizontal: true, flipVertical: null);

            editor.Preview.IsFlipAnimating.ShouldBeFalse("the previewed frame is not the one that flipped");
            frame.FlipHorizontal.ShouldBeFalse();
        }
    }
}
