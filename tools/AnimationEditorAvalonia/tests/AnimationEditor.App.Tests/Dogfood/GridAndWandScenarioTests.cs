using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The wireframe's grid and magic-wand modes under real pointer input: Ctrl+click in grid mode
/// adds a cell-sized frame (Ctrl+drag spans the dragged cells), double-click snaps the selected frame to a cell, and Ctrl+click with
/// the wand carves a frame out of an opaque blob.
/// </summary>
public class GridAndWandScenarioTests
{
    [AvaloniaFact]
    public async Task GridMode_CtrlClick_AddsAFrameCoveringTheClickedCell()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 128);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Click(editor.Control<ToggleButton>("SnapToGridCheck"));
        editor.TypeFlanker("GridSizeInput", "32");

        editor.ClickAt(editor.WireframePointAt(70, 40), RawInputModifiers.Control);

        walk.Frames.Count.ShouldBe(2);
        editor.PixelRectOf(walk.Frames[1]).ShouldBe((64, 32, 32, 32));
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);
    }

    [AvaloniaFact]
    public async Task GridMode_CtrlDrag_PreviewsThenAddsOneFrameSpanningTheDraggedCells()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 128);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Click(editor.Control<ToggleButton>("SnapToGridCheck"));
        editor.TypeFlanker("GridSizeInput", "32");
        Avalonia.Point from = editor.WireframePointAt(70, 40);
        Avalonia.Point to = editor.WireframePointAt(10, 100);
        int undoCountBefore = editor.UndoLabels.Count;

        // Drag up-left from cell (2,1) to cell (0,3): the span is columns 0..2, rows 1..3.
        editor.Window.MouseMove(from, RawInputModifiers.Control);
        editor.Window.MouseDown(from, MouseButton.Left, RawInputModifiers.Control);
        editor.Window.MouseMove(to, RawInputModifiers.Control | RawInputModifiers.LeftMouseButton);

        walk.Frames.Count.ShouldBe(1);
        editor.Wireframe.AddFrameGhost.ShouldBe(new SkiaSharp.SKRect(0, 32, 96, 128));

        editor.Window.MouseUp(to, MouseButton.Left, RawInputModifiers.Control);
        editor.Layout();

        walk.Frames.Count.ShouldBe(2);
        editor.PixelRectOf(walk.Frames[1]).ShouldBe((0, 32, 96, 96));
        editor.UndoLabels.Count.ShouldBe(undoCountBefore + 1);
    }

    [AvaloniaFact]
    public async Task GridMode_DoubleClickingACell_SnapsTheSelectedFrameToIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 128);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 128, 128)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.ClickRow(frame);
        editor.Click(editor.Control<ToggleButton>("SnapToGridCheck"));
        editor.TypeFlanker("GridSizeInput", "32");

        // A plain click must not resize a whole-sheet frame; the double-click is the gesture.
        editor.ClickAt(editor.WireframePointAt(100, 100));
        editor.PixelRectOf(frame).ShouldBe((0, 0, 128, 128));

        editor.DoubleClickAt(editor.WireframePointAt(100, 100));

        editor.PixelRectOf(frame).ShouldBe((96, 96, 32, 32));
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.PixelRectOf(frame).ShouldBe((0, 0, 128, 128));
    }

    [AvaloniaFact]
    public async Task MagicWand_CtrlClickOnABlob_AddsAFrameAroundIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePngWithBlob("sheet.png", 128, 128, (40, 24, 20, 30));
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 8, 8)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Click(editor.Control<ToggleButton>("MagicWandToggle"));
        editor.Wireframe.IsMagicWandMode.ShouldBeTrue();

        editor.ClickAt(editor.WireframePointAt(50, 40), RawInputModifiers.Control);

        walk.Frames.Count.ShouldBe(2);
        (int x, int y, int width, int height) = editor.PixelRectOf(walk.Frames[1]);
        (x, y).ShouldBe((40, 24));
        width.ShouldBeInRange(20, 21);
        height.ShouldBeInRange(30, 31);
        editor.ThrowIfErrorShown();
    }
}
