using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Delete, reorder, wireframe drag and Loop on a selection that mixes whole animations with
/// individual frames: every selected, unlocked item is acted on, and a lock skips only its own
/// items. Animation A is selected whole; a frame of animation B is ctrl-clicked in the tree.
/// </summary>
public class MixedSelectionScenarioTests
{
    private static async Task<(AnimationEditorHarness Editor, AnimationChainSave A, AnimationChainSave B)> OpenAsync(bool lockB)
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("A", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave a = editor.ChainNamed("A");
        AnimationChainSave b = editor.ChainNamed("B");
        if (lockB) editor.Click(editor.RowButton(b, "Lock Animation"));
        editor.ClickRow(a);
        editor.Expand(b);
        editor.ClickRow(b.Frames[0], RawInputModifiers.Control);
        return (editor, a, b);
    }

    [AvaloniaFact]
    public async Task Delete_AnimationPlusFrameOfAnother_DeletesBoth_AsOneUndoStep()
    {
        var (editor, a, b) = await OpenAsync(lockB: false);
        using var _ = editor;
        AnimationFrameSave b1 = b.Frames[0];
        int undoCount = editor.UndoLabels.Count;

        editor.Press(Key.Delete);

        editor.Project.AnimationChains.ShouldNotContain(a);
        b.Frames.ShouldNotContain(b1);
        editor.UndoLabels.Count.ShouldBe(undoCount + 1);

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.Project.AnimationChains.ShouldContain(a);
        b.Frames.Count.ShouldBe(2);
        b.Frames[0].ShouldBeSameAs(b1);
    }

    [AvaloniaFact]
    public async Task Delete_AnimationPlusFrameInLockedAnimation_DeletesOnlyTheAnimation()
    {
        var (editor, a, b) = await OpenAsync(lockB: true);
        using var _ = editor;

        editor.Press(Key.Delete);

        editor.Project.AnimationChains.ShouldNotContain(a);
        b.Frames.Count.ShouldBe(2);
    }

    private static (int X, int Y) At(AnimationEditorHarness editor, AnimationFrameSave frame)
    {
        var rect = editor.PixelRectOf(frame);
        return (rect.X, rect.Y);
    }

    [AvaloniaFact]
    public async Task WireframeDrag_MovesTheSelectedUnlockedFrame_WithTheSelectedAnimation()
    {
        var (editor, a, b) = await OpenAsync(lockB: false);
        using var _ = editor;

        editor.Drag(editor.WireframePointAt(8, 8), editor.WireframePointAt(16, 12));

        At(editor, a.Frames[0]).ShouldBe((8, 4));
        At(editor, a.Frames[1]).ShouldBe((24, 4));
        At(editor, b.Frames[0]).ShouldBe((8, 36), "the selected frame of B moves with A");
        At(editor, b.Frames[1]).ShouldBe((16, 32), "B's unselected frame stays");
    }

    [AvaloniaFact]
    public async Task WireframeDrag_LeavesTheSelectedFrameOfALockedAnimation()
    {
        var (editor, a, b) = await OpenAsync(lockB: true);
        using var _ = editor;

        editor.Drag(editor.WireframePointAt(8, 8), editor.WireframePointAt(16, 12));

        At(editor, a.Frames[0]).ShouldBe((8, 4));
        At(editor, b.Frames[0]).ShouldBe((0, 32));
    }

    [AvaloniaFact]
    public async Task WireframeDrag_LockedPrimaryAnimation_DoesNotBlockTheUnlockedOne()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        using var _ = editor;
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("A", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave a = editor.ChainNamed("A");
        AnimationChainSave b = editor.ChainNamed("B");
        editor.ClickRow(b);
        editor.Click(editor.RowButton(b, "Lock Animation"));
        editor.ClickRow(a, RawInputModifiers.Control);
        editor.Expand(a);
        editor.ClickRow(a.Frames[1], RawInputModifiers.Control);

        editor.Drag(editor.WireframePointAt(8, 8), editor.WireframePointAt(16, 12));

        At(editor, a.Frames[0]).ShouldBe((8, 4));
        At(editor, b.Frames[0]).ShouldBe((0, 32));
    }

    [AvaloniaFact]
    public async Task LoopCheckbox_AppliesToEverySelectedAnimation_AsOneUndoStep()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        using var _ = editor;
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("A", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "sheet.png", (0, 32, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave a = editor.ChainNamed("A");
        AnimationChainSave b = editor.ChainNamed("B");
        editor.ClickRow(a);
        editor.ClickRow(b, RawInputModifiers.Control);
        a.Loop.ShouldBeTrue();
        b.Loop.ShouldBeTrue();
        int undoCount = editor.UndoLabels.Count;

        editor.Click(editor.Control<CheckBox>("PropChainLoop"));

        a.Loop.ShouldBeFalse();
        b.Loop.ShouldBeFalse();
        editor.UndoLabels.Count.ShouldBe(undoCount + 1);
    }

    [AvaloniaFact]
    public async Task AltUp_MovesTheAnimationAndTheSelectedFrame_Together()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        using var _ = editor;
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("X", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("A", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "sheet.png", (0, 32, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave a = editor.ChainNamed("A");
        AnimationChainSave b = editor.ChainNamed("B");
        AnimationFrameSave a0 = a.Frames[0];
        AnimationFrameSave a1 = a.Frames[1];
        editor.Expand(a);
        editor.ClickRow(b);
        editor.ClickRow(a1, RawInputModifiers.Control);

        editor.Press(Key.Up, RawInputModifiers.Alt);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Project.AnimationChains.Select(c => c.Name).ShouldBe(new[] { "X", "B", "A" });
        a.Frames.ShouldBe(new[] { a1, a0 });
    }
}
