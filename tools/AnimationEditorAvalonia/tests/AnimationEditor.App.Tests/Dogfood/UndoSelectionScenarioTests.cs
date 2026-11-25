using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>What an undo leaves selected in the real window.</summary>
public class UndoSelectionScenarioTests
{
    private static AnimationChainSave FourFrames(string name) =>
        AnimationEditorHarness.Chain(name, "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16));

    [AvaloniaFact]
    public async Task AddPolygon_Undone_LeavesNoRemovedShapeSelected()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", FourFrames("Walk")));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add Polygon");
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.Services.SelectedState.SelectedShapes.ShouldBeEmpty();
        editor.Services.SelectedState.SelectedNodes.OfType<ShapeSave>().ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AddAnimation_Undone_LeavesNoRemovedAnimationSelected()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", FourFrames("Walk")));
        editor.Services.AppCommands.AddNewAnimationChain();
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.Services.SelectedState.SelectedNodes.OfType<AnimationChainSave>()
            .ShouldAllBe(c => editor.Project.AnimationChains.Contains(c));
    }
}
