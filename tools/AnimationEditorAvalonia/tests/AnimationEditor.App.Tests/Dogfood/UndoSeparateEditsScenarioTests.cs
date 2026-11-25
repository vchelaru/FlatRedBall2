using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Edits made one after the other are separate undo steps, and an undo after Save As goes to the new file.</summary>
public class UndoSeparateEditsScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationFrameSave Frame, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        string path = editor.WriteAchx("hero.achx", walk);
        await editor.OpenAsync(path);
        AnimationFrameSave frame = editor.ChainNamed("Walk").Frames[0];
        editor.Expand(editor.ChainNamed("Walk"));
        return (editor, frame, path);
    }

    [AvaloniaFact]
    public async Task OffsetXThenY_UndoTakesBackOnlyY()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(frame);

        editor.TypeNumber("PropRelX", "3");
        editor.TypeNumber("PropRelY", "4");
        editor.UndoManager.Undo();

        (frame.RelativeX, frame.RelativeY).ShouldBe((3f, 0f));
    }

    [AvaloniaFact]
    public async Task ShapeXThenName_UndoTakesBackOnlyTheName()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _ = editor;
        var rect = (AARectSave)frame.ShapesSave!.Shapes[0];
        editor.Expand(frame);
        editor.ClickRow(rect);

        editor.TypeNumber("PropRectX", "7");
        editor.TypeText("PropRectName", "Hurt");
        editor.UndoManager.Undo();

        (rect.Name, rect.X).ShouldBe(("Hit", 7f));
    }

    [AvaloniaFact]
    public async Task ShapeXThenMatchToFrame_UndoTakesBackOnlyTheMatch()
    {
        var (editor, frame, _) = await OpenAsync();
        using var _ = editor;
        var rect = (AARectSave)frame.ShapesSave!.Shapes[0];
        editor.Expand(frame);
        editor.ClickRow(rect);

        editor.TypeNumber("PropRectX", "7");
        editor.Services.AppCommands.MatchRectangleToFrame(rect, frame);
        editor.UndoManager.Undo();

        rect.X.ShouldBe(7f);
    }

    [AvaloniaFact]
    public async Task SaveAsInTheSameFolder_ThenUndo_WritesTheNewFileOnly()
    {
        var (editor, frame, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(frame);
        editor.TypeNumber("PropRelX", "3");
        string original = File.ReadAllText(path);
        string copy = Path.Combine(Path.GetDirectoryName(path)!, "copy.achj");
        editor.Dialogs.AnswerNextSaveFile(copy);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.UndoManager.Undo();

        AnimationEditorHarness.ReadSaved(copy).AnimationChains[0].Frames[0].RelativeX.ShouldBe(0f);
        File.ReadAllText(path).ShouldBe(original);
    }
}
