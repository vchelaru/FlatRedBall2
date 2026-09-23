using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The animation tree as a user works it: clicking rows, adding a chain with the + button and
/// naming it inline, deleting with the keyboard, undoing, and saving with Ctrl+S.
/// </summary>
public class ChainListScenarioTests
{
    [AvaloniaFact]
    public async Task AddChainButton_AddsAChainAndOpensInlineRename_TypingANameCommitsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);

        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));

        AnimationChainSave added = editor.Project.AnimationChains.Single(chain => chain.Name != "Walk");
        TreeNodeVm node = editor.NodeFor(added);
        node.IsEditing.ShouldBeTrue("the + button should open the new chain's name for editing");
        editor.Type("Run");
        editor.Press(Key.Enter);

        added.Name.ShouldBe("Run");
        editor.NodeFor(added).Header.ShouldBe("Run");
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(added);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ClickingAChainRow_SelectsItAndShowsItsFramesInTheInspector()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave run = editor.ChainNamed("Run");

        editor.ClickRow(run);

        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(run);
        editor.Control<Control>("PropChainPanel").IsVisible.ShouldBeTrue();
        editor.Expand(run);
        editor.ClickRow(run.Frames[0]);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(run.Frames[0]);
        editor.Control<Control>("PropFramePanel").IsVisible.ShouldBeTrue();
        editor.Control<NumericUpDown>("PropPixelX").Value.ShouldBe(32);
    }

    [AvaloniaFact]
    public async Task CtrlS_SavesTheEditedChainToDisk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Wireframe.BitmapSize.Width.ShouldBe(64, "selecting a frame loads its texture into the wireframe");

        editor.TypeNumber("PropPixelX", "8");
        editor.Control<NumericUpDown>("PropPixelX").Value.ShouldBe(8, "the typed value should be committed to the box");
        editor.Press(Key.S, RawInputModifiers.Control);

        // The editor holds UV in memory (8 of 64 pixels) and writes pixels to a pixel-coordinate file.
        walk.Frames[0].LeftCoordinate.ShouldBe(0.125f);
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        saved.CoordinateType.ShouldBe(TextureCoordinateType.Pixel);
        saved.AnimationChains.Single().Frames.Single().LeftCoordinate.ShouldBe(8);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task DeleteKey_RemovesTheSelectedChain_AndCtrlZBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.ClickRow(run);

        editor.Press(Key.Delete);

        editor.Project.AnimationChains.ShouldBe(new[] { walk });
        editor.DeletedToastText.ShouldBe("\"Run\" deleted");
        editor.Nodes.Select(node => node.Header).ShouldNotContain("Run");

        editor.Press(Key.Z, RawInputModifiers.Control);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
        editor.Nodes.Select(node => node.Header).ShouldContain("Run");
    }

    [AvaloniaFact]
    public async Task RightClickingAChain_OffersTheChainMenu_AndDeleteAnimationRemovesIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");

        editor.RightClickRow(walk);

        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(walk, "a right-click selects the row under the pointer");
        editor.TreeMenuHeaders.ShouldContain("Delete Animation");
        editor.TreeMenuHeaders.ShouldContain("Add Frame");
        editor.PickTreeMenuItem("Delete Animation");

        editor.Project.AnimationChains.ShouldBe(new[] { run });
        editor.ThrowIfErrorShown();
    }
}
