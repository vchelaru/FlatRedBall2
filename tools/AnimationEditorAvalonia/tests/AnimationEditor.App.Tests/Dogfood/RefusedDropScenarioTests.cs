using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Drops that are refused or land on the wrong side of a row.</summary>
public class RefusedDropScenarioTests : IDisposable
{
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "AeExplorerDrop", Guid.NewGuid().ToString("N"));

    public RefusedDropScenarioTests() => Directory.CreateDirectory(_outside);

    public void Dispose() => Directory.Delete(_outside, recursive: true);

    // The OS drag cannot run headless, so this raises what it ends with.
    private static async Task Drop(AnimationEditorHarness editor, Control target, Point point, params string[] paths)
    {
        var data = new DataTransfer();
        foreach (string path in paths)
        {
            IStorageItem item = Directory.Exists(path)
                ? (await editor.Window.StorageProvider.TryGetFolderFromPathAsync(path))!
                : (await editor.Window.StorageProvider.TryGetFileFromPathAsync(path))!;
            data.Add(DataTransferItem.CreateFile(item));
        }
        editor.Layout();
        target.RaiseEvent(new DragEventArgs(DragDrop.DragOverEvent, data, target, point, KeyModifiers.None));
        target.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, target, point, KeyModifiers.None));
        editor.Layout();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        editor.Layout();
    }

    private static Task DropOnRow(AnimationEditorHarness editor, object data, params string[] paths)
    {
        editor.Layout();
        Control row = editor.RowFor(data);
        return Drop(editor, editor.AnimTree, row.TranslatePoint(new Point(row.Bounds.Width / 2, 8), editor.AnimTree)!.Value, paths);
    }

    private static Task DropOnWindow(AnimationEditorHarness editor, params string[] paths) =>
        Drop(editor, editor.Window, new Point(300, 300), paths);

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Expand(editor.ChainNamed("Walk"));
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task APngThatIsNotAnImage_OnAFrame_IsRefused_AsInTheInspector()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string bad = Path.Combine(editor.ProjectFolder, "bad.png");
        File.WriteAllText(bad, "not an image");
        var frame = editor.ChainNamed("Walk").Frames[0];

        await DropOnRow(editor, frame, bad);

        frame.TextureName.ShouldBe("sheet.png");
        editor.UndoLabels.ShouldBeEmpty();
        editor.ProblemText.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task APngThatIsNotAnImage_OnAnAnimation_IsRefused()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string bad = Path.Combine(editor.ProjectFolder, "bad.png");
        File.WriteAllBytes(bad, []);

        await DropOnRow(editor, editor.ChainNamed("Walk"), bad);

        editor.ChainNamed("Walk").Frames.ShouldAllBe(f => f.TextureName == "sheet.png");
        editor.ProblemText.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task APngThatIsNotAnImage_OnTheCanvas_IsRefused()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string bad = Path.Combine(editor.ProjectFolder, "bad.png");
        File.WriteAllText(bad, "not an image");
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        await Drop(editor, editor.Wireframe, new Point(100, 100), bad);

        frame.TextureName.ShouldBe("sheet.png");
        editor.ProblemText.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public async Task APngThatIsNotAnImage_PickedInTheImagesList_IsRefused()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string bad = Path.Combine(editor.ProjectFolder, "bad.png").Replace('\\', '/');
        File.WriteAllText(bad, "not an image");
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);
        var combo = editor.Control<ComboBox>("TextureCombo");
        object? own = combo.SelectedItem;
        combo.Items.Add(bad);

        combo.SelectedItem = bad;
        editor.Layout();

        frame.TextureName.ShouldBe("sheet.png");
        editor.ProblemText.ShouldNotBeNull();
        combo.SelectedItem.ShouldBe(own);
    }

    // No answer is queued, so being asked to copy it would fail the scenario.
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APngFromOutsideThatIsNotAnImage_IsRefusedWithoutAskingToCopyIt(bool dropped)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string bad = Path.Combine(_outside, "bad.png");
        File.WriteAllText(bad, "not an image");
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        if (dropped) await DropOnRow(editor, frame, bad);
        else await editor.Window.ApplyPickedTextureAsync(bad);

        frame.TextureName.ShouldBe("sheet.png");
        File.Exists(Path.Combine(editor.ProjectFolder, "bad.png")).ShouldBeFalse();
        editor.ProblemText.ShouldNotBeNull().ShouldContain("Could not load image");
    }

    [AvaloniaFact]
    public async Task APngFromOutside_OnALockedAnimation_CopiesNothing()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string png = Path.Combine(_outside, "new.png");
        File.Move(editor.WritePng("new.png", 64, 64), png);
        editor.Services.AppCommands.SetChainLocked(editor.ChainNamed("Walk"), true);
        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return true;
        });

        await DropOnRow(editor, editor.ChainNamed("Walk").Frames[0], png);

        editor.ChainNamed("Walk").Frames[0].TextureName.ShouldBe("sheet.png");
        File.Exists(Path.Combine(editor.ProjectFolder, "new.png")).ShouldBeFalse();
        editor.ProblemText.ShouldNotBeNull().ShouldContain("locked");
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APngFromOutside_OnALockedAnimationRowOrTheCanvas_IsRefusedWithoutAsking(bool onCanvas)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string png = Path.Combine(_outside, "new.png");
        File.Move(editor.WritePng("new.png", 64, 64), png);
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk.Frames[0]);
        editor.Services.AppCommands.SetChainLocked(walk, true);

        if (onCanvas) await Drop(editor, editor.Wireframe, new Point(100, 100), png);
        else await DropOnRow(editor, walk, png);

        walk.Frames.ShouldAllBe(f => f.TextureName == "sheet.png");
        File.Exists(Path.Combine(editor.ProjectFolder, "new.png")).ShouldBeFalse();
        editor.ProblemText.ShouldNotBeNull().ShouldContain("locked");
    }
}
