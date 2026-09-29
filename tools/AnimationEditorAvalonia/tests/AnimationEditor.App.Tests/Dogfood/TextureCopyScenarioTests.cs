using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Assigning a PNG from outside the .achx's folder when a different file with the same name
/// already sits next to the .achx (#1249). Both entry points (Browse… and a PNG drop) share one
/// prompt, which must never replace that file unless the user picks overwrite.
/// </summary>
public class TextureCopyScenarioTests : IDisposable
{
    private readonly string _outsideFolder = Path.Combine(Path.GetTempPath(), "AeTextureCopy", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_outsideFolder)) Directory.Delete(_outsideFolder, recursive: true);
    }

    [AvaloniaFact]
    public async Task BrowseTexture_SameNamedFileAlreadyBesideAchx_UseExisting_LeavesItAndReferencesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string existing = editor.WritePng("hero.png", 32, 32, SKColors.Red);
        byte[] existingBytes = File.ReadAllBytes(existing);
        string outside = WriteOutsidePng("hero.png", SKColors.Blue);
        string path = editor.WriteAchx("anim.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        List<string> offered = new List<string>();
        editor.Dialogs.AnswerNextEditorDialog(content => ClickButton(content, "Use the existing", offered));

        await editor.Window.ApplyPickedTextureAsync(outside);

        offered.Count.ShouldBe(3, "a same-named, different file offers keep, overwrite and use-existing");
        walk.Frames[0].TextureName.ShouldBe("hero.png");
        File.ReadAllBytes(existing).ShouldBe(existingBytes, "the file beside the .achx must not be replaced");
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames[0].TextureName.ShouldBe("sheet.png");
    }

    [AvaloniaFact]
    public async Task PngDrop_SameNamedFileAlreadyBesideAchx_Overwrite_ReplacesItAndReferencesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string existing = editor.WritePng("hero.png", 32, 32, SKColors.Red);
        string outside = WriteOutsidePng("hero.png", SKColors.Blue);
        string path = editor.WriteAchx("anim.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Dialogs.AnswerNextEditorDialog(content => ClickButton(content, "Copy and overwrite", new List<string>()));

        // A real OS drop cannot be raised headlessly (see PngDropApplyTests), so call the one
        // apply path both drop surfaces share.
        MethodInfo drop = typeof(MainWindow).GetMethod("HandlePngDropAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool applied = await (Task<bool>)drop.Invoke(editor.Window, new object?[] { walk, walk.Frames[0], outside, false })!;

        applied.ShouldBeTrue();
        walk.Frames[0].TextureName.ShouldBe("hero.png");
        File.ReadAllBytes(existing).ShouldBe(File.ReadAllBytes(outside));
    }

    private string WriteOutsidePng(string name, SKColor color)
    {
        Directory.CreateDirectory(_outsideFolder);
        string path = Path.Combine(_outsideFolder, name);
        using SKBitmap bitmap = new SKBitmap(32, 32);
        bitmap.Erase(color);
        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static bool ClickButton(Control content, string labelStart, List<string> offered)
    {
        List<Button> buttons = content.GetVisualDescendants().OfType<Button>().ToList();
        offered.AddRange(buttons.Select(button => button.Content as string ?? ""));
        buttons.Single(button => (button.Content as string ?? "").StartsWith(labelStart))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return false;
    }
}
