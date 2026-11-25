using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Canvas gestures on locked, mixed and multiple selections: what moved is in the file and in one undo entry, what is locked stays.</summary>
public class CanvasMixedSelectionScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync(bool lockFirst)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePngWithBlob("sheet.png", 128, 128, (70, 70, 20, 20));
        var first = AnimationEditorHarness.Chain("First", "sheet.png", (0, 0, 16, 16), (0, 32, 16, 16));
        first.IsLocked = lockFirst;
        string path = editor.WriteAchx("hero.achx", first, AnimationEditorHarness.Chain("Second", "sheet.png", (48, 0, 16, 16), (48, 32, 16, 16)));
        await editor.OpenAsync(path);
        editor.Expand(editor.ChainNamed("First"));
        editor.Expand(editor.ChainNamed("Second"));
        return (editor, path);
    }

    private static string Regions(AnimationChainListSave list) => string.Join(" | ", list.AnimationChains.Select(chain => chain.Name + ": " +
        string.Join("; ", chain.Frames.Select(f => $"{f.LeftCoordinate},{f.TopCoordinate},{f.RightCoordinate},{f.BottomCoordinate}"))));

    private static void CheckFileAndUndo(AnimationEditorHarness editor, string path, string start, string? expected)
    {
        string now = Regions(editor.Project);
        if (expected is not null) now.ShouldBe(expected);
        Regions(AnimationEditorHarness.ReadSaved(path)).ShouldBe(now, "the file holds what the canvas shows");
        if (now == start) return;
        editor.UndoLabels.Count.ShouldBe(1, string.Join(" | ", editor.UndoLabels));
        editor.Press(Key.Z, RawInputModifiers.Control);
        Regions(editor.Project).ShouldBe(start);
        Regions(AnimationEditorHarness.ReadSaved(path)).ShouldBe(start);
        editor.Press(Key.Y, RawInputModifiers.Control);
        Regions(editor.Project).ShouldBe(now);
        Regions(AnimationEditorHarness.ReadSaved(path)).ShouldBe(now);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DragOfFramesOfTwoAnimations_OneLocked(bool lockFirst, bool clickSecondFirst)
    {
        var (editor, path) = await OpenAsync(lockFirst);
        using var _ = editor;
        string start = Regions(editor.Project);
        var a = editor.ChainNamed("First").Frames[0];
        var b = editor.ChainNamed("Second").Frames[0];
        editor.ClickRow(clickSecondFirst ? b : a);
        editor.ClickRow(clickSecondFirst ? a : b, RawInputModifiers.Control);

        editor.Drag(editor.WireframePointAt(56, 8), editor.WireframePointAt(60, 18));

        string moved = lockFirst ? "First: 0,0,16,16" : "First: 4,10,20,26";
        CheckFileAndUndo(editor, path, start, moved + "; 0,32,16,48 | Second: 52,10,68,26; 48,32,64,48");
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DragOfAnAnimationAndAFrameOfAnother_OneLocked(bool lockFirst, bool clickFrameFirst)
    {
        var (editor, path) = await OpenAsync(lockFirst);
        using var _ = editor;
        string start = Regions(editor.Project);
        var frame = editor.ChainNamed("Second").Frames[0];
        editor.ClickRow(clickFrameFirst ? frame : editor.ChainNamed("First"));
        editor.ClickRow(clickFrameFirst ? editor.ChainNamed("First") : frame, RawInputModifiers.Control);

        editor.Drag(editor.WireframePointAt(56, 8), editor.WireframePointAt(60, 18));

        CheckFileAndUndo(editor, path, start, null);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DragOfTwoAnimations_OneLocked(bool lockFirst)
    {
        var (editor, path) = await OpenAsync(lockFirst);
        using var _ = editor;
        string start = Regions(editor.Project);
        editor.ClickRow(editor.ChainNamed("First"));
        editor.ClickRow(editor.ChainNamed("Second"), RawInputModifiers.Control);

        editor.Drag(editor.WireframePointAt(56, 8), editor.WireframePointAt(60, 18));

        string moved = lockFirst ? "First: 0,0,16,16; 0,32,16,48" : "First: 4,10,20,26; 4,42,20,58";
        CheckFileAndUndo(editor, path, start, moved + " | Second: 52,10,68,26; 52,42,68,58");
    }

    [AvaloniaFact]
    public async Task GridDoubleClick_OnAFrameOfALockedAnimation_LeavesIt()
    {
        var (editor, path) = await OpenAsync(lockFirst: true);
        using var _ = editor;
        string start = Regions(editor.Project);
        editor.ClickRow(editor.ChainNamed("First").Frames[0]);
        editor.Click(editor.Control<ToggleButton>("SnapToGridCheck"));
        editor.TypeFlanker("GridSizeInput", "32");

        editor.DoubleClickAt(editor.WireframePointAt(70, 70));

        CheckFileAndUndo(editor, path, start, start);
    }

    [AvaloniaFact]
    public async Task WandDoubleClick_OnAFrameOfALockedAnimation_LeavesIt()
    {
        var (editor, path) = await OpenAsync(lockFirst: true);
        using var _ = editor;
        string start = Regions(editor.Project);
        editor.ClickRow(editor.ChainNamed("First").Frames[0]);
        editor.Click(editor.Control<ToggleButton>("MagicWandToggle"));
        editor.Hover(editor.WireframePointAt(80, 80));

        editor.DoubleClickAt(editor.WireframePointAt(80, 80));

        CheckFileAndUndo(editor, path, start, start);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CtrlClick_WithTwoAnimationsSelected_IsOneUndoEntry(bool lockFirst)
    {
        var (editor, path) = await OpenAsync(lockFirst);
        using var _ = editor;
        string start = Regions(editor.Project);
        editor.ClickRow(editor.ChainNamed("First"));
        editor.ClickRow(editor.ChainNamed("Second"), RawInputModifiers.Control);

        editor.ClickAt(editor.WireframePointAt(90, 90), RawInputModifiers.Control);

        editor.ChainNamed("Second").Frames.Count.ShouldBe(3);
        editor.ChainNamed("First").Frames.Count.ShouldBe(lockFirst ? 2 : 3);
        CheckFileAndUndo(editor, path, start, null);
    }

    [AvaloniaFact]
    public async Task CtrlClick_WithTwoAnimationsSelected_KeepsBothSelected()
    {
        var (editor, _) = await OpenAsync(lockFirst: false);
        using var disposed = editor;
        editor.ClickRow(editor.ChainNamed("First"));
        editor.ClickRow(editor.ChainNamed("Second"), RawInputModifiers.Control);
        string[] both = ["First", "Second"];
        string[] Selected() => editor.Services.SelectedState.SelectedChains.Select(c => c.Name).ToArray();

        editor.ClickAt(editor.WireframePointAt(90, 90), RawInputModifiers.Control);
        Selected().ShouldBe(both);
        editor.Press(Key.Z, RawInputModifiers.Control);
        Selected().ShouldBe(both);
        editor.Press(Key.Y, RawInputModifiers.Control);
        Selected().ShouldBe(both);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CtrlHover_WithALockedAndAnUnlockedAnimationSelected_ShowsTheFrameTheClickAdds(bool clickLockedFirst)
    {
        var (editor, _) = await OpenAsync(lockFirst: true);
        using var disposed = editor;
        editor.ClickRow(editor.ChainNamed(clickLockedFirst ? "First" : "Second"));
        editor.ClickRow(editor.ChainNamed(clickLockedFirst ? "Second" : "First"), RawInputModifiers.Control);

        editor.Window.MouseMove(editor.WireframePointAt(90, 90), RawInputModifiers.Control);

        editor.Wireframe.AddFrameGhost.ShouldNotBeNull();
        editor.Wireframe.IsShowingAddFrameCursor.ShouldBeTrue();
    }
}
