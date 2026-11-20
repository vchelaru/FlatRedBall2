using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>#1829: a Millisecond file plays at its real speed in the preview and the timeline.</summary>
public class FrameLengthUnitPlaybackScenarioTests
{
    private static async Task<AnimationEditorHarness> OpenWalkAsync(TimeMeasurementUnit unit)
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", unit,
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        return editor;
    }

    [AvaloniaFact]
    public async Task MillisecondFile_NonLoopingChain_StopsOnItsLastFrameWithinASecond()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync(TimeMeasurementUnit.Millisecond);
        ToggleButton loop = editor.Control<ToggleButton>("LoopToggle");
        if (loop.IsChecked == true)
        {
            editor.Click(loop);
        }
        if (!editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }

        (await editor.WaitUntilAsync(() => !editor.Preview.IsPlaying, TimeSpan.FromSeconds(2)))
            .ShouldBeTrue("three 100 ms frames end in 0.3 s; frames of 100 seconds would not end");

        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task MillisecondFile_ClickingTheTimelineMidway_ScrubsToTheMiddleFrame()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync(TimeMeasurementUnit.Millisecond);
        if (editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }
        Border surface = editor.Control<Border>("TimelineScrubSurface");

        editor.ClickAt(editor.PointIn(surface, surface.Bounds.Width * 0.5, surface.Bounds.Height / 2));

        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(1);
        editor.Preview.Playback.FrameElapsed.ShouldBeLessThan(0.1, "elapsed time inside a frame is in seconds");
    }

    [AvaloniaFact]
    public async Task MillisecondFile_PlaybackClock_RunsInSeconds()
    {
        using AnimationEditorHarness editor = await OpenWalkAsync(TimeMeasurementUnit.Millisecond);
        if (!editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }

        editor.Preview.Playback.Advance(0.15);

        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(1);
        editor.Preview.Playback.AnimTime.ShouldBeGreaterThanOrEqualTo(0.15);
    }
}
