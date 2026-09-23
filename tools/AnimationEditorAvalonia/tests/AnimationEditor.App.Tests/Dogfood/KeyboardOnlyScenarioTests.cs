using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The editor driven by the keyboard alone once a row has focus: arrow keys through an expanded
/// chain, Right and Left to expand and collapse, Home and End, Shift+Down range selection,
/// Enter on a row, F2 with the caret keys, F3, and Escape in the search box.
/// </summary>
public class KeyboardOnlyScenarioTests
{
    [AvaloniaFact]
    public async Task DownArrow_ThroughAnExpandedChain_WalksItsFrames_ThenTheNextChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk);

        editor.Press(Key.Down);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[0]);
        editor.Press(Key.Down);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1]);
        editor.Press(Key.Down);

        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe("Run");
        editor.Services.SelectedState.SelectedFrame.ShouldBeNull("Down past the last frame lands on the next chain's row");
        editor.Press(Key.Up);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[1], "Up walks back into the expanded chain");
    }

    [AvaloniaFact]
    public async Task EndAndHome_JumpToTheLastAndFirstRows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));

        editor.Press(Key.End);
        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe("Jump");
        editor.Press(Key.Home);
        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe("Walk");
    }

    [AvaloniaFact]
    public async Task Enter_OnACollapsedChainRow_ExpandsIt_AndAgainCollapsesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        TreeNodeVm node = editor.NodeFor(walk);
        node.IsExpanded.ShouldBeFalse();

        editor.Press(Key.Enter);
        node.IsExpanded.ShouldBeTrue();
        editor.Press(Key.Enter);

        node.IsExpanded.ShouldBeFalse();
        node.IsEditing.ShouldBeFalse("Enter on a row is not a rename");
        walk.Name.ShouldBe("Walk");
    }

    [AvaloniaFact]
    public async Task Escape_InTheSearchBox_ClearsTheFilter_AndCollapsesTheBox()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "Ru");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk", "Run" }, Case.Sensitive, "Walk stays visible only because it is selected");
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.VisibleChainHeaders.ShouldBe(new[] { "Run" });
        editor.Control<TextBox>("SearchBox").Focus();
        editor.Layout();

        editor.Press(Key.Escape);

        editor.Control<TextBox>("SearchBox").IsEffectivelyVisible.ShouldBeFalse("Escape collapses the box");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk", "Run" }, Case.Sensitive, "and clears the filter");
    }

    [AvaloniaFact]
    public async Task F2_ThenLeftArrow_MovesTheCaret_InsteadOfCollapsingTheChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk);
        TreeNodeVm node = editor.NodeFor(walk);

        editor.Press(Key.F2);
        node.IsEditing.ShouldBeTrue();
        editor.Press(Key.Left);
        node.IsEditing.ShouldBeTrue("Left moves the caret, it does not end the rename");
        node.IsExpanded.ShouldBeTrue("and it does not collapse the chain");
        editor.Press(Key.Home);
        editor.Type("Slow");
        editor.Press(Key.Enter);

        walk.Name.ShouldBe("SlowWalk");
        node.IsEditing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task F3_TogglesRenderDiagnostics_OnBothCanvases_AndTheMenuFollows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Wireframe.DiagnosticsEnabled.ShouldBeFalse();

        editor.Press(Key.F3);

        editor.Wireframe.DiagnosticsEnabled.ShouldBeTrue();
        editor.Preview.DiagnosticsEnabled.ShouldBeTrue();
        editor.Control<MenuItem>("MenuShowDiagnostics").IsChecked.ShouldBeTrue("the menu check mark follows the hotkey");
        editor.Press(Key.F3);
        editor.Wireframe.DiagnosticsEnabled.ShouldBeFalse();
        editor.Control<MenuItem>("MenuShowDiagnostics").IsChecked.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task LeftArrow_OnAFrame_MovesToItsChain_AndLeftAgainCollapsesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[1]);

        editor.Press(Key.Left);

        editor.Services.SelectedState.SelectedFrame.ShouldBeNull("Left on a frame goes up to the chain row");
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(walk);
        editor.Press(Key.Left);
        editor.NodeFor(walk).IsExpanded.ShouldBeFalse("Left on an expanded chain collapses it");
    }

    [AvaloniaFact]
    public async Task RightArrow_OnACollapsedChain_ExpandsIt_AndRightAgainEntersTheFirstFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.Right);
        editor.NodeFor(walk).IsExpanded.ShouldBeTrue();
        editor.Press(Key.Right);

        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[0], "Right on an expanded chain steps into its first frame");
    }

    [AvaloniaFact]
    public async Task ShiftDown_Twice_SelectsThreeFrames_AndDeleteRemovesThemAsOneStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.Press(Key.Down, RawInputModifiers.Shift);
        editor.Press(Key.Down, RawInputModifiers.Shift);
        editor.Services.SelectedState.SelectedFrames.Count.ShouldBe(3);
        editor.Press(Key.Delete);

        walk.Frames.Count.ShouldBe(1);
        editor.PixelRectOf(walk.Frames[0]).X.ShouldBe(48, "the unselected fourth frame is the one left");
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.Count.ShouldBe(4, "one undo brings all three back");
    }
}
