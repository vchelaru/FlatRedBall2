using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The chain's context menu and multi-selection: Duplicate with a flip, sort, the row's lock
/// button, Alt+Up, Ctrl+click selection with Delete, and copy/paste through the clipboard.
/// </summary>
public class ChainMenuScenarioTests
{
    [AvaloniaFact]
    public async Task AltUp_OnTheSecondChain_MovesItFirst_AndSavesTheOrder()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.ClickRow(run);

        editor.Press(Key.Up, RawInputModifiers.Alt);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run", "Walk" });
        editor.VisibleChainHeaders.ShouldBe(new[] { "Run", "Walk" });
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(run);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run", "Walk" });
    }

    [AvaloniaFact]
    public async Task CtrlC_ThenCtrlV_PastesACopyOfTheChain_WithAUniqueName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Count.ShouldBe(2);
        AnimationChainSave pasted = editor.Project.AnimationChains[1];
        pasted.Name.ShouldNotBe("Walk");
        pasted.Frames.Count.ShouldBe(2);
        pasted.Frames[1].LeftCoordinate.ShouldBe(walk.Frames[1].LeftCoordinate);
        editor.VisibleChainHeaders.ShouldContain(pasted.Name);
    }

    [AvaloniaFact]
    public async Task CtrlClickingTwoChains_ThenDelete_RemovesBoth_AndOneUndoRestoresBoth()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Jump"), RawInputModifiers.Control);
        editor.Services.SelectedState.SelectedChains.Count.ShouldBe(2);

        editor.Press(Key.Delete);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run", "Jump" });
    }

    [AvaloniaFact]
    public async Task DuplicateFlipHorizontal_AddsAMirroredCopy_AndSuggestsTheOppositeDirectionName()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("WalkLeft", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walkLeft = editor.ChainNamed("WalkLeft");

        editor.RightClickRow(walkLeft);
        editor.PickTreeMenuItem("Duplicate", "Flip Horizontal");

        editor.Project.AnimationChains.Count.ShouldBe(2);
        AnimationChainSave copy = editor.Project.AnimationChains[1];
        copy.Name.ShouldBe("WalkRight");
        copy.Frames.Single().FlipHorizontal.ShouldBeTrue();
        walkLeft.Frames.Single().FlipHorizontal.ShouldBeFalse("the source is left alone");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task RowLockButton_LocksTheChain_AndDeleteLeavesItsFramesAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Click(editor.RowButton(walk, "Lock Animation"));

        walk.IsLocked.ShouldBeTrue();
        editor.Control<CheckBox>("PropChainLocked").IsChecked.ShouldBe(true);
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Press(Key.Delete);
        walk.Frames.Count.ShouldBe(2, "a locked chain's frames cannot be deleted");
    }

    [AvaloniaFact]
    public async Task SortAnimationsAlphabetically_ReordersTheChains_AndUndoRestoresTheOldOrder()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.RightClickRow(editor.ChainNamed("Walk"));

        editor.PickTreeMenuItem("Sort Animations Alphabetically");

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Jump", "Run", "Walk" });
        editor.VisibleChainHeaders.ShouldBe(new[] { "Jump", "Run", "Walk" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Jump", "Run" });
    }
}
