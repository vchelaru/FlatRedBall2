using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The Ping-Pong and Reverse chain options (#1538), driven through the real window.</summary>
public class PlaybackModeScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenTwoChainsAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("A", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        await editor.OpenAsync(path);
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task PingPongCheckbox_AppliesToEverySelectedAnimation_AsOneUndoStep_AndSaves()
    {
        var (editor, path) = await OpenTwoChainsAsync();
        using var _ = editor;
        AnimationChainSave a = editor.ChainNamed("A");
        AnimationChainSave b = editor.ChainNamed("B");
        editor.ClickRow(a);
        editor.ClickRow(b, RawInputModifiers.Control);
        int undoCount = editor.UndoLabels.Count;

        editor.Click(editor.Control<CheckBox>("PropChainPingPong"));

        a.PingPong.ShouldBeTrue();
        b.PingPong.ShouldBeTrue();
        editor.UndoLabels.Count.ShouldBe(undoCount + 1);
        editor.UndoLabels.Last().ShouldBe("Enable ping-pong on 2 animations");
        editor.Control<ToggleButton>("PingPongToggle").IsChecked.ShouldBe(true);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.ShouldAllBe(c => c.PingPong);

        editor.Press(Key.Z, RawInputModifiers.Control);

        a.PingPong.ShouldBeFalse();
        b.PingPong.ShouldBeFalse();
        editor.Control<CheckBox>("PropChainPingPong").IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public async Task MixedSelection_ShowsBothControlsIndeterminate()
    {
        var (editor, _) = await OpenTwoChainsAsync();
        using var __ = editor;
        AnimationChainSave a = editor.ChainNamed("A");
        a.Reverse = true;
        editor.ClickRow(a);
        editor.ClickRow(editor.ChainNamed("B"), RawInputModifiers.Control);

        editor.Control<CheckBox>("PropChainReverse").IsChecked.ShouldBeNull();
        editor.Control<ToggleButton>("ReverseToggle").IsChecked.ShouldBeNull();
        editor.Control<CheckBox>("PropChainPingPong").IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public async Task ReverseToggle_SetsTheChain_AndThePreviewPlaysItBackward()
    {
        var (editor, path) = await OpenTwoChainsAsync();
        using var _ = editor;
        AnimationChainSave a = editor.ChainNamed("A");
        editor.ClickRow(a);

        editor.Click(editor.Control<ToggleButton>("ReverseToggle"));

        a.Reverse.ShouldBeTrue();
        editor.Control<CheckBox>("PropChainReverse").IsChecked.ShouldBe(true);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.First(c => c.Name == "A").Reverse.ShouldBeTrue();

        // The forward timeline mirrored: it starts at the end of the last frame.
        editor.Preview.Playback.Reset();
        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(2);
        editor.Preview.Playback.FrameElapsed.ShouldBe(0.1, 1e-6);
        editor.Preview.Playback.Play();
        editor.Preview.Playback.Advance(0.15);
        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(1);
        editor.Preview.Playback.FrameElapsed.ShouldBe(0.05, 1e-6);
    }

    [AvaloniaFact]
    public async Task ScrubbingAReverseChain_PutsThePlayheadWhereTheClickWas()
    {
        // The timeline is the forward timeline under Reverse too, so a click 95% across shows that forward time.
        var (editor, _) = await OpenTwoChainsAsync();
        using var __ = editor;
        AnimationChainSave a = editor.ChainNamed("A");
        a.Reverse = true;
        editor.ClickRow(a);
        if (editor.Preview.IsPlaying) editor.Press(Key.Space);
        Border surface = editor.Control<Border>("TimelineScrubSurface");

        editor.ClickAt(editor.PointIn(surface, surface.Bounds.Width * 0.95, surface.Bounds.Height / 2));

        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(2);
        editor.Preview.Playback.FrameElapsed.ShouldBeGreaterThan(0.05);
    }

    [AvaloniaFact]
    public async Task PingPongChain_KeyframeLaneHoldsTheLastKeyframeInsteadOfWrapping()
    {
        var (editor, _) = await OpenTwoChainsAsync();
        using var __ = editor;
        AnimationChainSave a = editor.ChainNamed("A");
        for (int i = 0; i < a.Frames.Count; i++)
        {
            a.Frames[i].RelativeX = i * 10;
            a.Frames[i].OffsetInterpolation = "Linear";
        }
        a.PingPong = true;
        editor.ClickRow(a);

        var lane = editor.Control<AnimationEditor.Views.Controls.KeyframeLaneControl>("FirstLane").Lane!;

        lane.Spans.Count.ShouldBe(2);
        lane.Holds.Count.ShouldBe(1);
    }
}
