using AnimationEditor.DocumentMatrix;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Cut, then paste in the same document, and what may change in between.</summary>
public class CutPasteScenarioTests
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
    public async Task CutAnimationUndoAndRedo_WriteTheFileEachTime()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(editor.ChainNamed("Walk"));
        string before = Documents(path);
        Press(editor, Key.X);
        editor.ClickRow(editor.ChainNamed("Run"));
        Press(editor, Key.V);
        string after = Documents(path);

        Press(editor, Key.Z);
        Documents(path).ShouldBe(before);
        Press(editor, Key.Y);
        Documents(path).ShouldBe(after);

        static string Documents(string path) => AnimationEditorHarness.ReadSaved(path).ToXmlString();
    }

    [AvaloniaFact]
    public async Task CutPastedTwice_SecondPasteIsACopy()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        Press(editor, Key.X);
        editor.ClickRow(editor.ChainNamed("Run"));
        Press(editor, Key.V);
        Press(editor, Key.V);

        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(c => c.Frames.Count).ShouldBe([1, 3]);
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
    public async Task Tsx_CutFramePastedInTheSameAnimation_StaysOnItsTile()
    {
        var (editor, path) = await OpenTilesetAsync();
        using var _ = editor;
        var blink = editor.ChainNamed("Blink");
        editor.Expand(blink);
        editor.ClickRow(blink.Frames[0]);
        Press(editor, Key.X);
        editor.ClickRow(blink.Frames[1]);

        Press(editor, Key.V);

        Documents.TileAnimations(path).ShouldBe(["3: 6@100 5@100", "7: 8@100 9@100"]);
    }

    [AvaloniaFact]
    public async Task Tsx_CopyPasteAndDuplicate_LeaveTheSourceOnItsTile()
    {
        var (editor, path) = await OpenTilesetAsync();
        using var _ = editor;
        string before = File.ReadAllText(path);
        editor.ClickRow(editor.ChainNamed("Blink"));
        Press(editor, Key.C);
        Press(editor, Key.V);
        editor.ClickRow(editor.ChainNamed("Torch"));
        Press(editor, Key.D);

        Documents.TileAnimations(path).ShouldBe(["3: 5@100 6@100", "5: 5@100 6@100", "7: 8@100 9@100", "8: 8@100 9@100"]);
        Press(editor, Key.Z);
        Press(editor, Key.Z);
        File.ReadAllText(path).ShouldBe(before);
    }
}
