using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The preview player: Space and the play button toggle playback, time advances frames, the
/// loop toggle stops a non-looping chain at its end, and clicking the timeline scrubs.
/// </summary>
public class PlaybackScenarioTests
{
    [AvaloniaFact]
    public async Task ClickingTheTimelineNearItsEnd_ScrubsToTheLastFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        if (editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }
        Border surface = editor.Control<Border>("TimelineScrubSurface");

        editor.ClickAt(editor.PointIn(surface, surface.Bounds.Width * 0.95, surface.Bounds.Height / 2));

        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(2);
        editor.Preview.IsPlaying.ShouldBeFalse("scrubbing does not start playback");
    }

    [AvaloniaFact]
    public async Task LoopOff_LetsTheChainStopOnItsLastFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        ToggleButton loop = editor.Control<ToggleButton>("LoopToggle");
        if (loop.IsChecked == true)
        {
            editor.Click(loop);
        }
        editor.Preview.Loop.ShouldBeFalse();
        if (!editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }

        (await editor.WaitUntilAsync(() => !editor.Preview.IsPlaying, TimeSpan.FromSeconds(2))).ShouldBeTrue("a non-looping chain stops at its end");

        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task PlayButton_TogglesPlayback_AndTimeAdvancesTheFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        Button play = editor.Control<Button>("PlayPauseBtn");
        if (editor.Preview.IsPlaying)
        {
            editor.Click(play);
        }
        editor.Preview.IsPlaying.ShouldBeFalse();
        int frameBefore = editor.Preview.Playback.CurrentFrameIndex;

        editor.Click(play);
        editor.Preview.IsPlaying.ShouldBeTrue();
        (await editor.WaitUntilAsync(() => editor.Preview.Playback.CurrentFrameIndex != frameBefore, TimeSpan.FromSeconds(2)))
            .ShouldBeTrue("playback should advance past the first frame within two seconds");

        editor.Click(play);
        editor.Preview.IsPlaying.ShouldBeFalse();
        int frameAtPause = editor.Preview.Playback.CurrentFrameIndex;
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(frameAtPause, "a paused preview does not advance");
    }

    [AvaloniaFact]
    public async Task Space_WithTheTreeFocused_TogglesPlayback()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        bool playingBefore = editor.Preview.IsPlaying;

        editor.Press(Key.Space);
        editor.Preview.IsPlaying.ShouldBe(!playingBefore);

        editor.Press(Key.Space);
        editor.Preview.IsPlaying.ShouldBe(playingBefore);
    }

    [AvaloniaFact]
    public async Task SpeedField_TypingTwo_DoublesThePlaybackSpeed()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));

        editor.TypeFlanker("SpeedInput", "2");

        editor.Preview.SpeedMultiplier.ShouldBe(2);
    }
}
