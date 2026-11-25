using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.IO;
using AnimationEditor.DocumentMatrix;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Cut, then paste in the same document, and what may change in between.</summary>
public class CutPasteSplashScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        return (editor, path);
    }

    private static void Press(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
    }

    private static IEnumerable<string> SavedNames(string path) =>
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(c => c.Name);

    [AvaloniaFact]
    public async Task CutAnimation_PastedInTheSameFile_KeepsItsName()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));
        Press(editor, Key.X);
        editor.ClickRow(editor.ChainNamed("Run"));

        Press(editor, Key.V);

        SavedNames(path).ShouldBe(["Run", "Walk"]);
    }

    [AvaloniaFact]
    public async Task CutShape_PastedOnItsOwnFrame_KeepsItsName()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.Services.AppCommands.AddShape(walk.Frames[0], new AARectSave { Name = "Hit" });
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        editor.ClickRow(walk.Frames[0].ShapesSave!.Shapes[0]);
        Press(editor, Key.X);

        Press(editor, Key.V);

        AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames[0].ShapesSave!.AARectSaves.Single().Name.ShouldBe("Hit");
    }

    [AvaloniaFact]
    public async Task EditBetweenCutAndPaste_IsKept()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        Press(editor, Key.X);
        editor.Services.AppCommands.SetAllFrameLengths(walk, 0.5f);
        editor.ClickRow(editor.ChainNamed("Run"));

        Press(editor, Key.V);

        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single(c => c.Name.StartsWith("Walk")).Frames
            .Select(f => f.FrameLength).ShouldBe([0.5f, 0.5f]);
    }

    [AvaloniaFact]
    public async Task EditOfACutFrameBeforeThePaste_IsKept()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        Press(editor, Key.X);
        editor.Services.AppCommands.SetAllFrameLengths(walk, 0.5f);
        editor.ClickRow(editor.ChainNamed("Run"));

        Press(editor, Key.V);

        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single(c => c.Name == "Run").Frames.Last().FrameLength.ShouldBe(0.5f);
    }

    [AvaloniaFact]
    public async Task ClipboardReplacedByAnotherEditorAfterACut_PasteDoesNotDeleteTheCutAnimation()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));
        Press(editor, Key.X);
        // What a Copy in a second editor window leaves on the clipboard.
        await editor.Window.Clipboard!.SetTextAsync(ClipboardPayload.Serialize(
            [new AnimationChainSave { Name = "Jump", Frames = { new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.1f, RightCoordinate = 8, BottomCoordinate = 8 } } }],
            FrameLengthUnit.Of(editor.Project)));
        editor.ClickRow(editor.ChainNamed("Run"));

        Press(editor, Key.V);

        SavedNames(path).ShouldBe(["Walk", "Run", "Jump"]);
    }

    [AvaloniaFact]
    public async Task EditBetweenCutAndPasteIntoAnotherTab_IsKept()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        Press(editor, Key.X);
        editor.Services.AppCommands.SetAllFrameLengths(walk, 0.5f);
        editor.ClickTab("other.achx");

        Press(editor, Key.V);

        SavedNames(path).ShouldBe(["Run"]);
        AnimationEditorHarness.ReadSaved(other).AnimationChains.Single(c => c.Name == "Walk").Frames
            .Select(f => f.FrameLength).ShouldBe([0.5f, 0.5f]);
    }

    [AvaloniaFact]
    public async Task ClipboardReplacedAfterACut_PasteIntoAnotherTab_DoesNotDeleteTheCutAnimation()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(other);
        editor.ClickTab("hero.achx");
        editor.ClickRow(editor.ChainNamed("Walk"));
        Press(editor, Key.X);
        await editor.Window.Clipboard!.SetTextAsync(ClipboardPayload.Serialize(
            [AnimationEditorHarness.Chain("Jump", "sheet.png", (0, 0, 8, 8))], FrameLengthUnit.Of(editor.Project)));
        editor.ClickTab("other.achx");

        Press(editor, Key.V);

        SavedNames(path).ShouldBe(["Walk", "Run"]);
    }

    [AvaloniaFact]
    public async Task CutRenamedBeforeThePaste_KeepsTheNewName()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        Press(editor, Key.X);
        editor.Services.AppCommands.RenameChain(walk, "Stride");
        editor.ClickRow(editor.ChainNamed("Run"));

        Press(editor, Key.V);

        SavedNames(path).ShouldBe(["Run", "Stride"]);
    }

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenTilesetAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = Path.Combine(editor.ProjectFolder, "Heroes.tsx");
        File.WriteAllText(path, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="sheet.png" width="64" height="64"/>
             <tile id="3">
              <properties>
               <property name="Name" value="Blink"/>
              </properties>
              <animation>
               <frame tileid="5" duration="100"/>
               <frame tileid="6" duration="100"/>
              </animation>
             </tile>
             <tile id="7">
              <properties>
               <property name="Name" value="Torch"/>
              </properties>
              <animation>
               <frame tileid="8" duration="100"/>
               <frame tileid="9" duration="100"/>
              </animation>
             </tile>
            </tileset>
            """);
        await editor.OpenAsync(path);
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task Tsx_CutAnimationPastedInTheSameTileset_StaysOnItsTile()
    {
        var (editor, path) = await OpenTilesetAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Blink"));
        Press(editor, Key.X);
        editor.ClickRow(editor.ChainNamed("Torch"));

        Press(editor, Key.V);

        Documents.TileAnimations(path).ShouldBe(["3: 5@100 6@100", "7: 8@100 9@100"]);
    }
}
