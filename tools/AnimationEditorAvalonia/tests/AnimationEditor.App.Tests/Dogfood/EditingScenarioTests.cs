using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Everyday editing a user expects to just work: cut and paste frames between chains and chains
/// between tabs, redo, arrow-key navigation, the remaining chain and shape menu items, offsets,
/// the preview toggles, and the recent-files menu.
/// </summary>
public class EditingScenarioTests
{
    [AvaloniaFact]
    public async Task ArrowDown_InTheTree_MovesTheSelectionToTheNextChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));

        editor.Press(Key.Down);

        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(editor.ChainNamed("Run"));
        editor.Control<Control>("PropChainPanel").IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task CrossTabCopyPaste_PutsTheChainInTheOtherDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        await editor.OpenAsync(enemy);
        editor.ClickRow(editor.ChainNamed("Bite"));
        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ClickTab(editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith("hero.achx")).DisplayName);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Bite" });
        AnimationEditorHarness.ReadSaved(hero).AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Bite" });
        AnimationEditorHarness.ReadSaved(enemy).AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Bite" }, "the source is untouched");
    }

    [AvaloniaFact]
    public async Task CtrlX_OnAFrame_ThenCtrlV_OnAnotherChain_MovesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        float cutLeft = walk.Frames[1].LeftCoordinate;
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[1]);

        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ClickRow(run);
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ThrowIfErrorShown();
        walk.Frames.Count.ShouldBe(1, "cut removes the frame from its chain");
        run.Frames.Count.ShouldBe(2);
        run.Frames[1].LeftCoordinate.ShouldBe(cutLeft);
    }

    [AvaloniaFact]
    public async Task CtrlY_AndCtrlShiftZ_BothRedo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Click(editor.RowButton(walk, "Add Frame"));
        editor.Click(editor.RowButton(walk, "Add Frame"));
        walk.Frames.Count.ShouldBe(3);
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.Count.ShouldBe(1);

        editor.Press(Key.Y, RawInputModifiers.Control);
        walk.Frames.Count.ShouldBe(2);
        editor.Press(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
        walk.Frames.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task DeleteFrameMenuItem_RemovesTheRightClickedFrame_NotTheSelectedOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave first = walk.Frames[0];
        AnimationFrameSave last = walk.Frames[2];
        editor.Expand(walk);
        editor.ClickRow(first);

        editor.RightClickRow(last);
        editor.PickTreeMenuItem("Delete Frame");

        walk.Frames.ShouldContain(first);
        walk.Frames.ShouldNotContain(last);
    }

    [AvaloniaFact]
    public async Task DuplicateOnARectangle_AddsASecondRectangleToTheSameFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = walk.Frames[0].ShapesSave!.AARectSaves.Single();
        editor.ClickRow(rect);

        editor.Press(Key.D, RawInputModifiers.Control);

        editor.ThrowIfErrorShown();
        walk.Frames[0].ShapesSave!.AARectSaves.Count().ShouldBe(2);
        walk.Frames[0].ShapesSave!.AARectSaves.Select(shape => shape.Name).Distinct().Count().ShouldBe(2, "the copy gets its own name");
    }

    [AvaloniaFact]
    public async Task FlipVertically_FlipsEveryFrame_AndInvertFrameOrderReversesThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave first = walk.Frames[0];

        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Flip Vertically");
        walk.Frames.ShouldAllBe(frame => frame.FlipVertical);

        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Invert Frame Order");
        walk.Frames[2].ShouldBeSameAs(first);
        editor.NodeFor(first).Header.ShouldBe("Frame 3");

        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames[0].ShouldBeSameAs(first);
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.ShouldAllBe(frame => !frame.FlipVertical);
    }

    [AvaloniaFact]
    public async Task MatchFrameSize_MakesTheRectangleCoverTheFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 20)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = walk.Frames[0].ShapesSave!.AARectSaves.Single();
        editor.TypeNumber("PropRectScaleX", "3");

        editor.RightClickRow(rect);
        editor.PickTreeMenuItem("Match Frame Size");

        (rect.ScaleX * 2, rect.ScaleY * 2).ShouldBe((32f, 20f), "scale is the half-size, so twice it is the frame's pixel size");
    }

    [AvaloniaFact]
    public async Task PreviewToggles_FollowTheToolbarButtons()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        bool onionBefore = editor.Preview.ShowOnionSkin;
        bool originBefore = editor.Preview.ShowOrigin;
        bool interpolateBefore = editor.Preview.InterpolateOffsets;

        editor.Click(editor.Control<ToggleButton>("OnionSkinToggle"));
        editor.Click(editor.Control<ToggleButton>("ShowOriginCheck"));
        editor.Click(editor.Control<ToggleButton>("InterpolateToggle"));

        editor.Preview.ShowOnionSkin.ShouldBe(!onionBefore);
        editor.Preview.ShowOrigin.ShouldBe(!originBefore);
        editor.Preview.InterpolateOffsets.ShouldBe(!interpolateBefore);
    }

    [AvaloniaFact]
    public async Task RecentFilesMenu_ListsTheOpenedFile_AndClickingItFocusesItsTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        await editor.OpenAsync(enemy);
        MenuItem recent = editor.Control<MenuItem>("MenuLoadRecent");
        List<string> headers = recent.Items.OfType<MenuItem>().Select(item => (string)item.Header!).ToList();
        headers.ShouldBe(new[] { "enemy.achx", "hero.achx" });

        editor.ClickMenu(recent.Items.OfType<MenuItem>().Single(item => (string)item.Header! == "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero.achx");
        editor.Tabs.Tabs.Count.ShouldBe(2, "an already open file is focused, not opened twice");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task RelativeXField_MovesTheFrameOffset_AndSaves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.TypeNumber("PropRelX", "-7.5");

        walk.Frames[0].RelativeX.ShouldBe(-7.5f);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().RelativeX.ShouldBe(-7.5f);
    }
}
