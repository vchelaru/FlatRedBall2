using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

public class SaveAsToAnotherFolderScenarioTests
{
    // #2031
    [AvaloniaFact]
    public async Task SaveAs_AnotherFolder_TheTabKeepsItsImages()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16))));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        string moved = Path.Combine(Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "moved")).FullName, "hero.achx");

        editor.Dialogs.AnswerNextSaveFile(moved);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        // Leaving the box unedited puts its text back on the frame.
        editor.Control<TextBox>("PropTextureName").Text.ShouldBe("../sheet.png");
        editor.Control<TextBox>("PropTextureName").Focus();
        editor.Layout();
        editor.Press(Key.Enter);
        editor.ClickRow(walk);
        editor.NodeFor(walk).Thumbnail.ShouldNotBeNull();
        editor.ClickRow(walk.Frames[1]);
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));

        editor.Press(Key.Delete);
        editor.Press(Key.Z, RawInputModifiers.Control);
        // Nothing from before the Save As is left to undo.
        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.ThrowIfErrorShown();
        AnimationEditorHarness.ReadSaved(moved).AnimationChains.Single().Frames.Select(frame => frame.TextureName)
            .ShouldBe(["../sheet.png", "../sheet.png"]);
    }
}
