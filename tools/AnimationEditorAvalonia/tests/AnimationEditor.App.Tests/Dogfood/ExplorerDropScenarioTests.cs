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

/// <summary>Files and folders dropped from the file manager on unusual targets.</summary>
public class ExplorerDropScenarioTests : IDisposable
{
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "AeExplorerDrop", Guid.NewGuid().ToString("N"));

    public ExplorerDropScenarioTests() => Directory.CreateDirectory(_outside);

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

    [AvaloniaTheory]
    [InlineData("art")]
    [InlineData("art.png")]
    [InlineData("art.achx")]
    [InlineData("art.seqj")]
    [InlineData("art.tsx")]
    public async Task AFolder_OnARowAndOnTheWindow_ChangesNothing_AndDoesNotThrow(string name)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string folder = Directory.CreateDirectory(Path.Combine(_outside, name)).FullName;
        string before = File.ReadAllText(path);

        await DropOnRow(editor, editor.ChainNamed("Walk").Frames[0], folder);
        await DropOnWindow(editor, folder);

        File.ReadAllText(path).ShouldBe(before);
        editor.TabLabels.Count.ShouldBe(1);
        editor.ChainNamed("Walk").Frames[0].TextureName.ShouldBe("sheet.png");
    }

    [AvaloniaTheory]
    [InlineData("bad.achx")]
    [InlineData("bad.achj")]
    [InlineData("bad.tsx")]
    [InlineData("bad.seqj")]
    public async Task ABrokenFileThenAGoodOne_OnTheWindow_OpensTheGoodOne_AndSaysTheOtherIsBroken(string bad)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string broken = Path.Combine(editor.ProjectFolder, bad);
        File.WriteAllText(broken, "<broken");
        string good = editor.WriteAchx("good.achx", AnimationEditorHarness.Chain("Good", "sheet.png", (0, 0, 16, 16)));

        await DropOnWindow(editor, broken, good, good);

        editor.TabLabels.Count.ShouldBe(2, string.Join(", ", editor.TabLabels));
        editor.Project.AnimationChains.Single().Name.ShouldBe("Good");
        editor.ProblemText.ShouldNotBeNull();
    }

    [AvaloniaTheory]
    [InlineData("a%20b#c.achx")]
    [InlineData("h [1] +&.achx")]
    public async Task AFileWithAnUnusualName_OnTheWindow_Opens(string name)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string file = editor.WriteAchx(name, AnimationEditorHarness.Chain("Odd", "sheet.png", (0, 0, 16, 16)));

        await DropOnWindow(editor, file);

        editor.Project.AnimationChains.Single().Name.ShouldBe("Odd");
    }

    [AvaloniaFact]
    public async Task APngWithAnUnusualName_OnAFrame_IsStoredByItsName()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string png = editor.WritePng("a%20b#c.png", 64, 64);

        await DropOnRow(editor, editor.ChainNamed("Walk").Frames[0], png);

        editor.ChainNamed("Walk").Frames[0].TextureName.ShouldBe("a%20b#c.png");
        AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames[0].TextureName.ShouldBe("a%20b#c.png");
    }
}
