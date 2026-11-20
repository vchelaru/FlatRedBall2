using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>#1829: copy and cut in a file of one unit, paste in a file of another, through the real tabs and hotkeys.</summary>
public class FrameLengthUnitPasteScenarioTests
{
    private static async Task<(string Path, AnimationChainSave Chain)> OpenFileAsync(
        AnimationEditorHarness editor, string name, TimeMeasurementUnit unit)
    {
        string path = editor.WriteAchx(name, unit,
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave chain = editor.ChainNamed("Run");
        editor.Expand(chain);
        return (path, chain);
    }

    [AvaloniaFact]
    public async Task CopyFrameFromAMillisecondFile_PasteIntoASecondsFile_KeepsTheDuration()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        (_, AnimationChainSave source) = await OpenFileAsync(editor, "ms.achx", TimeMeasurementUnit.Millisecond);
        source.Frames[0].FrameLength.ShouldBe(100f);
        editor.ClickRow(source.Frames[0]);
        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        (string targetPath, AnimationChainSave target) = await OpenFileAsync(editor, "sec.achx", TimeMeasurementUnit.Second);
        editor.ClickRow(target);
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        target.Frames.Count.ShouldBe(3);
        target.Frames[2].FrameLength.ShouldBe(0.1f, 0.0001f);
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(targetPath);
        saved.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Second);
        saved.AnimationChains.Single().Frames[2].FrameLength.ShouldBe(0.1f, 0.0001f);
    }

    [AvaloniaFact]
    public async Task CopyAnimationFromASecondsFile_PasteIntoAMillisecondFile_ConvertsEveryFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        (_, AnimationChainSave source) = await OpenFileAsync(editor, "sec.achx", TimeMeasurementUnit.Second);
        source.Frames[1].FrameLength = 0.25f;
        editor.ClickRow(source);
        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        (string targetPath, AnimationChainSave target) = await OpenFileAsync(editor, "ms.achx", TimeMeasurementUnit.Millisecond);
        editor.ClickRow(target);
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        AnimationChainSave pasted = editor.Project.AnimationChains.Last();
        ReferenceEquals(pasted, target).ShouldBeFalse();
        pasted.Frames.Select(f => f.FrameLength).ShouldBe(new[] { 100f, 250f }, tolerance: 0.01f);
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(targetPath);
        saved.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
    }

    [AvaloniaFact]
    public async Task PasteWithinOneMillisecondFile_KeepsTheLength()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        (_, AnimationChainSave chain) = await OpenFileAsync(editor, "ms.achx", TimeMeasurementUnit.Millisecond);
        editor.ClickRow(chain.Frames[0]);
        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ClickRow(chain);
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        chain.Frames.Count.ShouldBe(3);
        chain.Frames[2].FrameLength.ShouldBe(100f, 0.01f);
    }
}
