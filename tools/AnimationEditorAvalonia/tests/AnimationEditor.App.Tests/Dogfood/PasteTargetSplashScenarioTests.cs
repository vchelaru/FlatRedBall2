using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Where a paste goes for each kind of selected row, and what it does to the files it touches.</summary>
public class PasteTargetSplashScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        string path = editor.WriteAchx("hero.achx", walk,
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("Idle", "sheet.png", (48, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (48, 16, 16, 16)));
        await editor.OpenAsync(path);
        return (editor, path);
    }

    private static async Task<string> CutWalkAndSwitchToOtherAsync(AnimationEditorHarness editor)
    {
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");
        editor.ClickRow(editor.ChainNamed("Walk"));
        Press(editor, Key.X);
        editor.ClickTab("other.achx");
        return other;
    }

    private static void Press(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
    }

    private static List<AnimationChainSave> Saved(string path) => AnimationEditorHarness.ReadSaved(path).AnimationChains;

    [AvaloniaFact]
    public async Task MoveDownInTheMenuOfAMultiSelection_MovesTheWholeSelection()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Run"), RawInputModifiers.Control);
        editor.RightClickRow(editor.ChainNamed("Walk"));

        editor.PickTreeMenuItem("Move Down");

        Saved(path).Select(c => c.Name).ShouldBe(["Idle", "Walk", "Run", "Jump"]);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopiedAnimation_IsPastedAfterTheSelectedOne_AfterAReloadToo(bool reload)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));
        Press(editor, Key.C);
        editor.ClickRow(editor.ChainNamed("Idle"));
        if (reload) editor.Services.AppCommands.ReloadAchxFromDisk(path);
        editor.Layout();
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Idle");

        Press(editor, Key.V);

        Saved(path).Select(c => c.Name).ShouldBe(["Walk", "Run", "Idle", "Walk2", "Jump"]);
    }
}
