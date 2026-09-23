using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The wireframe (texture) panel under real pointer input: clicking a frame box selects the
/// frame, dragging inside it moves it, dragging a corner resizes it, Ctrl+click adds a frame,
/// and the wheel zooms. The existing wireframe tests call the control's Simulate* methods;
/// these go through the window so pointer routing is part of what is tested.
/// </summary>
public class WireframeScenarioTests
{
    private const int Sheet = 128;

    [AvaloniaFact]
    public async Task ClickingEmptyTexture_WhileAFrameIsSelected_KeepsTheSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32), (64, 0, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Wireframe.BitmapSize.Width.ShouldBe(Sheet);

        // With one frame selected the wireframe draws only that frame's box, so the other
        // frame's area is plain texture: a click there is not a way to switch frames.
        editor.ClickAt(editor.WireframeRectOf(walk.Frames[1]).Center);

        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[0]);
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ClickingAFrameBox_WhileTheChainIsSelected_StartsNoDragAndRecordsNoUndo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32), (64, 0, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        // With the whole chain selected a press inside any box grabs the chain for a drag (#719),
        // so a plain click must not change the selection, move anything, or leave an undo entry.
        editor.ClickAt(editor.WireframeRectOf(walk.Frames[1]).Center);

        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(walk);
        editor.Services.SelectedState.SelectedFrame.ShouldBeNull();
        editor.PixelRectOf(walk.Frames[1]).ShouldBe((64, 0, 32, 32));
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DoubleClickingAFrameBox_WhileTheChainIsSelected_SelectsThatFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32), (64, 0, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.DoubleClickAt(editor.WireframeRectOf(walk.Frames[1]).Center);

        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DoubleClickingAFrameBox_WhileSeveralChainsAreSelected_SelectsThatFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32), (64, 0, 32, 32)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 64, 32, 32), (64, 64, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.ClickRow(walk);
        editor.ClickRow(run, RawInputModifiers.Control);

        editor.DoubleClickAt(editor.WireframeRectOf(run.Frames[1]).Center);

        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(run.Frames[1]);
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(run);
        // The multi-chain bag must be replaced, or the tree keeps every chain highlighted.
        editor.Services.SelectedState.SelectedChains.ShouldBeEmpty();
        editor.AnimTree.SelectedItems.OfType<TreeNodeVm>().Select(n => n.Data).ShouldBe(new object[] { run.Frames[1] });
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DoubleClickingAFrameBox_AfterShiftSelectingEveryChain_SelectsThatFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32), (64, 0, 32, 32)),
            AnimationEditorHarness.Chain("Idle", "sheet.png", (0, 128, 32, 32)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 64, 32, 32), (64, 64, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.ClickRow(walk);
        editor.ClickRow(run, RawInputModifiers.Shift);

        editor.DoubleClickAt(editor.WireframeRectOf(run.Frames[1]).Center);

        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(run.Frames[1]);
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(run);
        // The multi-chain bag must be replaced, or the tree keeps every chain highlighted.
        editor.Services.SelectedState.SelectedChains.ShouldBeEmpty();
        editor.AnimTree.SelectedItems.OfType<TreeNodeVm>().Select(n => n.Data).ShouldBe(new object[] { run.Frames[1] });
    }

    [AvaloniaFact]
    public async Task CtrlClickOnEmptyTexture_AddsAFrameThere_AndUndoRemovesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.ClickAt(editor.WireframePointAt(96, 96), RawInputModifiers.Control);

        walk.Frames.Count.ShouldBe(2);
        (int x, int y, int width, int height) = editor.PixelRectOf(walk.Frames[1]);
        (width, height).ShouldBe((32, 32), "the new frame takes the last frame's size");
        (x + width / 2, y + height / 2).ShouldBe((96, 96), "centred on the click");
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);

        editor.Press(Key.Z, RawInputModifiers.Control);

        walk.Frames.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task DraggingACornerHandle_ResizesTheFrame_AsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (16, 16, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.ClickRow(frame);
        int undoBefore = editor.UndoManager.UndoHistory.Count;

        Rect box = editor.WireframeRectOf(frame);
        Point bottomRight = new Point(box.Right, box.Bottom);
        Point target = editor.WireframePointAt(80, 80);
        editor.Drag(bottomRight, target);

        editor.PixelRectOf(frame).ShouldBe((16, 16, 64, 64));
        editor.UndoManager.UndoHistory.Count.ShouldBe(undoBefore + 1, "one drag is one undo step");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.PixelRectOf(frame).ShouldBe((16, 16, 32, 32));
    }

    [AvaloniaFact]
    public async Task DraggingInsideTheSelectedFrame_MovesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.ClickRow(frame);

        editor.Drag(editor.WireframePointAt(16, 16), editor.WireframePointAt(48, 40));

        editor.PixelRectOf(frame).ShouldBe((32, 24, 32, 32));
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(frame);
        editor.UndoLabels.First().ShouldNotBeNullOrEmpty();
    }

    [AvaloniaFact]
    public async Task DraggingAFrameOfALockedChain_LeavesItAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.ClickRow(walk);
        editor.Click(editor.Control<ToggleButton>("PropChainLocked"));
        editor.Expand(walk);
        editor.ClickRow(frame);

        editor.Drag(editor.WireframePointAt(16, 16), editor.WireframePointAt(48, 40));

        editor.PixelRectOf(frame).ShouldBe((0, 0, 32, 32));
    }

    [AvaloniaFact]
    public async Task CtrlWheelOverTheWireframe_ZoomsIn_AndTheZoomBoxFollows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        float zoomBefore = editor.Wireframe.CameraState.Zoom;

        editor.Wheel(editor.WireframePointAt(64, 64), 1, RawInputModifiers.Control);
        (await editor.WaitUntilAsync(() => !editor.Wireframe.IsZoomAnimating, TimeSpan.FromSeconds(2))).ShouldBeTrue("the smooth zoom settles");

        editor.Wireframe.CameraState.Zoom.ShouldBeGreaterThan(zoomBefore);
        editor.Control<AnimationEditor.App.Controls.ZoomControl>("WireframeZoom").Text
            .ShouldBe($"{(int)MathF.Round(editor.Wireframe.CameraState.Zoom * 100)}%");
    }
}
