using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Undo in the real window: what it puts back.</summary>
public class UndoFidelityScenarioTests
{
    private static AnimationChainSave FourFrames(string name) =>
        AnimationEditorHarness.Chain(name, "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16));

    [AvaloniaFact]
    public async Task Delete_FramesCtrlClickedLastFirst_UndoPutsThemBackInOrder()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", FourFrames("Walk"));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[2]);
        editor.ClickRow(walk.Frames[0], RawInputModifiers.Control);

        editor.Press(Key.Delete);
        walk.Frames.Count.ShouldBe(2);
        editor.Press(Key.Z, RawInputModifiers.Control);

        AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames.Select(f => f.LeftCoordinate).ShouldBe([0f, 16f, 32f, 48f]);
    }

    [AvaloniaFact]
    public async Task Delete_AnimationsCtrlClickedLastFirst_UndoPutsThemBackInOrder()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", FourFrames("A"), FourFrames("B"), FourFrames("C"), FourFrames("D"));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("C"));
        editor.ClickRow(editor.ChainNamed("A"), RawInputModifiers.Control);

        editor.Press(Key.Delete);
        editor.Project.AnimationChains.Count.ShouldBe(2);
        editor.Press(Key.Z, RawInputModifiers.Control);

        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(c => c.Name).ShouldBe(["A", "B", "C", "D"]);
    }
}
