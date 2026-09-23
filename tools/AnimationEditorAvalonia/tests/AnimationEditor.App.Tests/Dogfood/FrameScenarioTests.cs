using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Frames as a user edits them: the row's + button, the context menu, Alt+Arrow reorder, Ctrl+D
/// duplicate, Delete and undo, and the inspector's frame fields.
/// </summary>
public class FrameScenarioTests
{
    [AvaloniaFact]
    public async Task AltDown_OnAFrame_MovesItAfterItsSibling_AndUndoMovesItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave first = walk.Frames[0];
        editor.Expand(walk);
        editor.ClickRow(first);

        editor.Press(Key.Down, RawInputModifiers.Alt);

        walk.Frames.IndexOf(first).ShouldBe(1);
        editor.Nodes.First(node => ReferenceEquals(node.Data, first)).Header.ShouldBe("Frame 2");
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(first, "the moved frame stays selected");

        editor.Press(Key.Z, RawInputModifiers.Control);

        walk.Frames.IndexOf(first).ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task ContextMenuAddFrame_AppendsAFrameOnTheChainsTexture()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Add Frame");

        walk.Frames.Count.ShouldBe(2);
        walk.Frames[1].TextureName.ShouldBe("sheet.png");
        editor.UndoLabels.First().ShouldContain("Frame");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task CtrlD_OnAFrame_DuplicatesItRightAfterItself()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.Press(Key.D, RawInputModifiers.Control);

        walk.Frames.Count.ShouldBe(3);
        walk.Frames[1].LeftCoordinate.ShouldBe(walk.Frames[0].LeftCoordinate);
        walk.Frames[1].ShouldNotBeSameAs(walk.Frames[0]);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1], "the duplicate is selected");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task DeleteKey_OnAFrame_RemovesIt_AndUndoRestoresItAtTheSameIndex()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave middle = walk.Frames[1];
        editor.Expand(walk);
        editor.ClickRow(middle);

        editor.Press(Key.Delete);

        walk.Frames.Count.ShouldBe(2);
        walk.Frames.ShouldNotContain(middle);
        editor.DeletedToastText.ShouldNotBeNull();

        editor.Press(Key.Z, RawInputModifiers.Control);

        walk.Frames.IndexOf(middle).ShouldBe(1);
        editor.Nodes.Count(node => node.IsFrameNode).ShouldBe(3, "the tree shows the restored frame");
    }

    [AvaloniaFact]
    public async Task FlipHorizontalToggle_FlipsTheSelectedFrame_AndSavesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.Click(editor.Control<ToggleButton>("PropFlipH"));

        walk.Frames[0].FlipHorizontal.ShouldBeTrue();
        editor.Press(Key.S, RawInputModifiers.Control);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().FlipHorizontal.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task FrameLengthField_TypingAValue_SetsTheFrameLength_AndUndoRestoresIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.TypeFlanker("PropFrameLen", "0.25");

        walk.Frames[0].FrameLength.ShouldBe(0.25f);
        editor.UndoManager.CanUndo.ShouldBeTrue();

        editor.Press(Key.Z, RawInputModifiers.Control);

        walk.Frames[0].FrameLength.ShouldBe(0.1f);
    }

    [AvaloniaFact]
    public async Task RowAddFrameButton_AppendsAFrame_AndSelectsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Click(editor.RowButton(walk, "Add Frame"));

        walk.Frames.Count.ShouldBe(2);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);
        editor.NodeFor(walk.Frames[1]).Header.ShouldBe("Frame 2");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ShiftClickingFrames_SelectsTheRange_AndDeleteRemovesThemAll()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave last = walk.Frames[3];
        editor.Expand(walk);

        editor.ClickRow(walk.Frames[0]);
        editor.ClickRow(walk.Frames[2], RawInputModifiers.Shift);

        editor.Services.SelectedState.SelectedFrames.Count.ShouldBe(3);

        editor.Press(Key.Delete);

        walk.Frames.ShouldBe(new[] { last });
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.Count.ShouldBe(4, "one undo brings the whole range back");
    }

    [AvaloniaFact]
    public async Task TextureNameField_TypingAnotherPng_RetargetsTheFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WritePng("other.png", 32, 32);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.TypeText("PropTextureName", "other.png");

        walk.Frames[0].TextureName.ShouldBe("other.png");
        editor.Wireframe.BitmapSize.ShouldBe((32, 32), "the wireframe follows the frame's new texture");
        editor.ThrowIfErrorShown();
    }
}
