using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Undo in the document an item was cut from, once it was pasted into another tab.</summary>
public class CutIntoAnotherTabUndoScenarioTests
{
    private static AnimationChainSave FourFrames(string name) =>
        AnimationEditorHarness.Chain(name, "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16));

    private static async Task<(AnimationEditorHarness Editor, string Source)> OpenTwoAsync(AnimationChainSave first, AnimationChainSave second)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string source = editor.WriteAchx("source.achx", first, second);
        string target = editor.WriteAchx("target.achx", FourFrames("Target"));
        await editor.OpenAsync(target);
        await editor.OpenAsync(source);
        return (editor, source);
    }

    private static void CutIntoTarget(AnimationEditorHarness editor, Func<object> targetRow)
    {
        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.ClickTab("target.achx");
        editor.ClickRow(targetRow());
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ClickTab("source.achx");
        editor.Wait(TimeSpan.FromMilliseconds(50));
    }

    private static IEnumerable<string> SavedNames(string path) =>
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(c => c.Name);

    [AvaloniaFact]
    public async Task CutAnimation_UndoInTheSource_BringsItBackThenUndoesWhatCameBefore()
    {
        var (editor, source) = await OpenTwoAsync(FourFrames("Walk"), FourFrames("Idle"));
        using var _ = editor;
        editor.Services.AppCommands.SortAnimationsAlphabetically();
        editor.ClickRow(editor.ChainNamed("Walk"));

        CutIntoTarget(editor, () => editor.ChainNamed("Target"));
        SavedNames(source).ShouldBe(["Idle"]);

        editor.UndoLabels[^1].ShouldBe("Cut Animation 'Walk'");
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedNames(source).ShouldBe(["Idle", "Walk"]);
        editor.Press(Key.Z, RawInputModifiers.Control);
        SavedNames(source).ShouldBe(["Walk", "Idle"]);
        editor.Press(Key.Y, RawInputModifiers.Control);
        editor.Press(Key.Y, RawInputModifiers.Control);
        SavedNames(source).ShouldBe(["Idle"]);
    }

    // The way back when the paste is undone too.
    [AvaloniaFact]
    public async Task CutAnimation_UndoInBothTabs_LeavesItOnlyInTheSource()
    {
        var (editor, source) = await OpenTwoAsync(FourFrames("Walk"), FourFrames("Idle"));
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));

        CutIntoTarget(editor, () => editor.ChainNamed("Target"));
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.ClickTab("target.achx");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Press(Key.Z, RawInputModifiers.Control);

        SavedNames(source).ShouldBe(["Walk", "Idle"]);
        SavedNames(Path.Combine(Path.GetDirectoryName(source)!, "target.achx")).ShouldBe(["Target"]);
    }

    [AvaloniaFact]
    public async Task CutFrame_UndoInTheSource_BringsItBackWhereItWas()
    {
        var (editor, source) = await OpenTwoAsync(FourFrames("Walk"), FourFrames("Idle"));
        using var _ = editor;
        editor.Expand(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Walk").Frames[1]);

        CutIntoTarget(editor, () => editor.ChainNamed("Target"));
        AnimationEditorHarness.ReadSaved(source).AnimationChains[0].Frames.Count.ShouldBe(3);
        editor.Press(Key.Z, RawInputModifiers.Control);

        AnimationEditorHarness.ReadSaved(source).AnimationChains[0].Frames.Select(f => f.LeftCoordinate).ShouldBe([0f, 16f, 32f, 48f]);
    }

    [AvaloniaFact]
    public async Task CutShape_UndoInTheSource_BringsItBackThenUndoesWhatCameBefore()
    {
        var walk = FourFrames("Walk");
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "One" });
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Two" });
        var (editor, source) = await OpenTwoAsync(walk, FourFrames("Idle"));
        using var _ = editor;
        AnimationFrameSave frame = editor.ChainNamed("Walk").Frames[0];
        var one = (AARectSave)frame.ShapesSave!.Shapes[0];
        editor.Services.AppCommands.MoveShape(one, frame, 1);
        editor.Expand(editor.ChainNamed("Walk"));
        editor.Expand(frame);
        editor.ClickRow(one);

        CutIntoTarget(editor, () =>
        {
            editor.Expand(editor.ChainNamed("Target"));
            return editor.ChainNamed("Target").Frames[0];
        });
        IEnumerable<string> Saved() => AnimationEditorHarness.ReadSaved(source).AnimationChains[0].Frames[0].ShapesSave!.Shapes.Cast<ShapeSave>().Select(s => s.Name);
        Saved().ShouldBe(["Two"]);

        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved().ShouldBe(["Two", "One"]);
        editor.Press(Key.Z, RawInputModifiers.Control);
        Saved().ShouldBe(["One", "Two"]);
    }

    // A background save of a .tsx decides the tiles anew.
    [AvaloniaFact]
    public async Task CutAnimationOutOfATileset_UndoInTheSource_LeavesTheFileAsItWas()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string target = editor.WriteAchx("target.achx", FourFrames("Target"));
        string source = Path.Combine(Path.GetDirectoryName(target)!, "source.tsx");
        File.WriteAllText(source, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="source" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="sheet.png" width="64" height="64"/>
             <tile id="7">
              <animation>
               <frame tileid="8" duration="100"/>
               <frame tileid="9" duration="100"/>
              </animation>
             </tile>
            </tileset>
            """);
        await editor.OpenAsync(target);
        await editor.OpenAsync(source);
        var chain = editor.Project.AnimationChains.Single();
        editor.Services.AppCommands.DuplicateChains([chain]);
        string before = File.ReadAllText(source);
        editor.ClickRow(chain);

        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.ClickTab("target.achx");
        editor.ClickRow(editor.ChainNamed("Target"));
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        File.ReadAllText(source).ShouldNotBe(before);
        editor.ClickTab("source.tsx");
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Press(Key.Z, RawInputModifiers.Control);

        File.ReadAllText(source).ShouldBe(before);
    }
}
