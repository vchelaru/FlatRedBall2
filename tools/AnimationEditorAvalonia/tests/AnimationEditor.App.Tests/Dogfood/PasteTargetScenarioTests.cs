using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Where a paste goes for each kind of selected row, and what it does to the files it touches.</summary>
public class PasteTargetScenarioTests
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
    public async Task CopiedShape_PastedWhileItIsSelected_IsAddedToItsFrame()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.Expand(editor.ChainNamed("Walk"));
        editor.Expand(frame);
        editor.ClickRow(frame.ShapesSave!.Shapes[0]);
        Press(editor, Key.C);

        Press(editor, Key.V);

        Saved(path)[0].Frames[0].ShapesSave!.AARectSaves.Select(r => r.Name).ShouldBe(["Hit", "Hit2"]);
    }

    [AvaloniaFact]
    public async Task CopiedFrame_PastedWhileAShapeIsSelected_GoesIntoThatShapesAnimation()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        editor.ClickRow(walk.Frames[1]);
        Press(editor, Key.C);
        editor.ClickRow(walk.Frames[0].ShapesSave!.Shapes[0]);

        Press(editor, Key.V);

        Saved(path).Select(c => c.Frames.Count).ShouldBe([3, 1, 1, 1]);
    }

    [AvaloniaFact]
    public async Task CutSourceChangedOnDiskBehindTheTab_PasteKeepsTheOutsideChange()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string other = await CutWalkAndSwitchToOtherAsync(editor);
        string outside = File.ReadAllText(path).Replace("<Name>Idle</Name>", "<Name>IdleOutside</Name>");
        File.WriteAllText(path, outside);

        Press(editor, Key.V);

        File.ReadAllText(path).ShouldBe(outside);
        Saved(other).Select(c => c.Name).ShouldBe(["Bite", "Walk"]);
    }

    [AvaloniaFact]
    public async Task CutSourceTabClosed_PasteIsACopy()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string other = await CutWalkAndSwitchToOtherAsync(editor);
        editor.CloseTab("hero.achx");

        Press(editor, Key.V);
        Press(editor, Key.V);

        Saved(path).Count.ShouldBe(4);
        Saved(other).Select(c => c.Name).ShouldBe(["Bite", "Walk", "Walk2"]);
    }

    [AvaloniaFact]
    public async Task CutSourceReloadedWithAnotherAnimation_PasteIsACopy()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));
        Press(editor, Key.X);
        File.WriteAllText(path, File.ReadAllText(path).Replace("<Name>Idle</Name>", "<Name>IdleOutside</Name>"));
        editor.Services.AppCommands.ReloadAchxFromDisk(path);
        editor.Layout();
        editor.ClickRow(editor.ChainNamed("Run"));

        Press(editor, Key.V);

        // In any order: where it lands after a reload is #2132.
        Saved(path).Select(c => c.Name).ShouldBe(["Walk", "Run", "Walk2", "IdleOutside", "Jump"], ignoreOrder: true);
    }

    [AvaloniaFact]
    public async Task LockedAnimation_CopyAndDuplicate_AgreeOnTheLock()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.Click(editor.RowButton(editor.ChainNamed("Run"), "Lock Animation"));
        editor.ClickRow(editor.ChainNamed("Run"));
        Press(editor, Key.D);
        editor.ClickRow(editor.ChainNamed("Run"));
        Press(editor, Key.C);
        Press(editor, Key.V);

        var locks = editor.Project.AnimationChains.Where(c => c.Name.StartsWith("Run")).Select(c => c.IsLocked).ToList();
        locks.Count.ShouldBe(3);
        locks.Distinct().Count().ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task AnimationAndOneOfItsFrames_DeletedTogether_UndoRestoresTheFile()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        string before = AnimationEditorHarness.ReadSaved(path).ToXmlString();
        editor.ClickRow(walk.Frames[1]);
        editor.ClickRow(walk, RawInputModifiers.Control);
        editor.ClickRow(editor.ChainNamed("Jump"), RawInputModifiers.Control);

        editor.Press(Key.Delete);
        Saved(path).Select(c => c.Name).ShouldBe(["Run", "Idle"]);
        Press(editor, Key.Z);
        AnimationEditorHarness.ReadSaved(path).ToXmlString().ShouldBe(before);
        Press(editor, Key.Y);
        Saved(path).Select(c => c.Name).ShouldBe(["Run", "Idle"]);
    }
}
