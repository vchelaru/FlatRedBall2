using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using AnimationEditor.Views.Tests;
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
        string bad = Path.Combine(editor.ProjectFolder, "bad.png");
        File.WriteAllText(bad, "not an image");
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);
        var combo = editor.Control<ComboBox>("TextureCombo");
        object? own = combo.SelectedItem;

        PickInImagesList(editor, bad);

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

    [AvaloniaFact]
    public async Task AFrameDrop_OnTheLowerHalfOfARowWithItsShapesShown_LandsAfterThatFrame()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var chain = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16));
        chain.Frames[1].ShapesSave = new ShapesSave();
        chain.Frames[1].ShapesSave!.Add(new AARectSave { Name = "A", ScaleX = 4, ScaleY = 4 });
        chain.Frames[1].ShapesSave!.Add(new AARectSave { Name = "B", ScaleX = 4, ScaleY = 4 });
        await editor.OpenAsync(editor.WriteAchx("hero.achx", chain));
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[1]);
        var moved = walk.Frames[0];
        var target = walk.Frames[1];

        var header = editor.RowHeaderPoint(target);
        Point inTree = editor.Window.TranslatePoint(new Point(header.X, header.Y + 6), editor.AnimTree)!.Value;
        typeof(MainWindow).GetField("_pendingFrameDrag", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor.Window,
            new AnimationEditor.Core.DragDrop.FrameDragSource(new[] { moved }, walk, AnimationEditor.Core.DragDrop.FrameDragValidity.Valid));
        var format = typeof(MainWindow).GetField("FrameDragDataFormat", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create((DataFormat<string>)format, "frame"));
        editor.AnimTree.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, editor.AnimTree, inTree, KeyModifiers.None));
        editor.Layout();

        walk.Frames.IndexOf(moved).ShouldBe(1);
        walk.Frames.IndexOf(target).ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task AnAnimationDrop_OnTheLowerHalfOfAnExpandedAnimationsRow_LandsAfterThatAnimation()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        var walk = editor.ChainNamed("Walk");
        var run = editor.ChainNamed("Run");
        editor.Expand(run);
        var header = editor.RowHeaderPoint(run);
        Point inTree = editor.Window.TranslatePoint(new Point(header.X, header.Y + 6), editor.AnimTree)!.Value;
        typeof(MainWindow).GetField("_pendingChainDrag", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor.Window,
            new AnimationEditor.Core.DragDrop.ChainDragSource(new[] { walk }, AnimationEditor.Core.DragDrop.ChainDragValidity.Valid));
        var format = typeof(MainWindow).GetField("ChainDragDataFormat", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create((DataFormat<string>)format, "chain"));
        editor.AnimTree.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, editor.AnimTree, inTree, KeyModifiers.None));
        editor.Layout();

        editor.Project.AnimationChains.Select(c => c.Name).ShouldBe(["Run", "Walk"]);
    }

    // The copy question is a real dialog here: the drop event has gone on to the window before it is answered.
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APngAndAnAnimationFile_OnAFrame_TheAnimationFileDoesNotOpenUnderTheCopyQuestion(bool onCanvas)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        typeof(MainWindow).GetField("_dialogHost", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(editor.Window, new AnimationEditor.Views.Dialogs.WindowEditorDialogHost(editor.Window));
        string png = Path.Combine(_outside, "new.png");
        File.Move(editor.WritePng("new.png", 64, 64), png);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "sub"));
        string other = editor.WriteAchx(Path.Combine("sub", "other.achx"), AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        if (onCanvas) await Drop(editor, editor.Wireframe, new Point(100, 100), png, other);
        else await DropOnRow(editor, frame, png, other);
        string openUnderTheQuestion = editor.Project.AnimationChains[0].Name;
        editor.Window.OwnedWindows.Single().GetVisualDescendants().OfType<Button>()
            .First(b => (b.Content as string ?? "").StartsWith("Copy")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await editor.WaitUntilAsync(() => frame.TextureName == "new.png", TimeSpan.FromSeconds(5));

        openUnderTheQuestion.ShouldBe("Walk");
        AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames[0].TextureName.ShouldBe("new.png");
    }

    // Nothing was read, so it is not known to be no image: the copy says why it failed and offers Retry (#2076).
    [AvaloniaFact]
    public async Task APngFromOutsideInUse_IsStillAskedAbout_AndRetryApplies()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string png = Path.Combine(_outside, "held.png");
        File.Move(editor.WritePng("held.png", 64, 64), png);
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.Dialogs.AnswerNextEditorDialog(content =>
        {
            content.GetVisualDescendants().OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return true;
        });

        using (new FileStream(png, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await DropOnRow(editor, frame, png);
        string? problem = editor.ProblemText;
        editor.Click(editor.Notifications.ErrorBannerRetryBtn);
        await editor.WaitUntilAsync(() => frame.TextureName == "held.png", TimeSpan.FromSeconds(5));

        problem.ShouldNotBeNull().ShouldStartWith("Could not copy: ");
        frame.TextureName.ShouldBe("held.png");
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APngInTheProjectInUse_IsTaken(bool dropped)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string png = editor.WritePng("held.png", 64, 64);
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        using (new FileStream(png, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            if (dropped) await DropOnRow(editor, frame, png);
            else PickInImagesList(editor, png);
        }

        frame.TextureName.ShouldBe("held.png");
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APngTooLargeToShow_SaysItsSize(bool dropped)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string huge = Path.Combine(editor.ProjectFolder, "huge.png");
        File.Copy(PngHeaderFile.Write("huge.png", 24000, 24000), huge);
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        if (dropped) await DropOnRow(editor, frame, huge);
        else PickInImagesList(editor, huge);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("24000 x 24000");
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APngAndAnAnimationFile_WhereNothingTakesThePng_OpensTheAnimationFileWithoutAsking(bool onCanvas)
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string png = Path.Combine(_outside, "new.png");
        File.Move(editor.WritePng("new.png", 64, 64), png);
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));

        if (onCanvas)
        {
            editor.Services.SelectedState.SelectedFrame = null;
            editor.Services.SelectedState.SelectedChain = null;
            await Drop(editor, editor.Wireframe, new Point(100, 100), png, other);
        }
        else
        {
            await Drop(editor, editor.AnimTree, new Point(60, editor.AnimTree.Bounds.Height - 6), png, other);
        }

        editor.Project.AnimationChains.Single().Name.ShouldBe("Other");
        editor.Dialogs.Shown.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task PickingInTheImagesList_OnALockedAnimation_SaysSo_AndGoesBack()
    {
        var (editor, path) = await OpenAsync();
        using var _ = editor;
        string other = editor.WritePng("other.png", 32, 32);
        var walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk.Frames[0]);
        editor.Services.AppCommands.SetChainLocked(walk, true);
        var combo = editor.Control<ComboBox>("TextureCombo");
        object? own = combo.SelectedItem;

        PickInImagesList(editor, other);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("'Walk' is locked");
        combo.SelectedItem.ShouldBe(own);
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));
    }

    private static void PickInImagesList(AnimationEditorHarness editor, string file)
    {
        var combo = editor.Control<ComboBox>("TextureCombo");
        string item = file.Replace('\\', '/');
        combo.Items.Add(item);
        combo.SelectedItem = item;
        editor.Layout();
    }
}
