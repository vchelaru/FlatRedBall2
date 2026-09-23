using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// More prodding: bulk inspector edits, the colour fields, edits racing a hot reload, deleting
/// what the search filter hides, deep copies, sixty frames, very long names, and Space with a
/// toolbar toggle focused.
/// </summary>
public class QaRoundTwoScenarioTests
{
    [AvaloniaFact]
    public async Task BulkFrameLength_WithTwoFramesSelected_SetsBoth_AsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.ClickRow(walk.Frames[1], RawInputModifiers.Shift);
        editor.Services.SelectedState.SelectedFrames.Count.ShouldBe(2);

        editor.TypeFlanker("PropFrameLen", "0.3");

        walk.Frames[0].FrameLength.ShouldBe(0.3f);
        walk.Frames[1].FrameLength.ShouldBe(0.3f);
        walk.Frames[2].FrameLength.ShouldBe(0.1f);
        editor.Press(Key.Z, RawInputModifiers.Control);
        (walk.Frames[0].FrameLength, walk.Frames[1].FrameLength).ShouldBe((0.1f, 0.1f));
    }

    [AvaloniaFact]
    public async Task BulkSelectionWithDifferentLengths_ShowsMixed_AndTypingNothingChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        fixture.Frames[1].FrameLength = 0.25f;
        string path = editor.WriteAchx("hero.achx", fixture);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.ClickRow(walk.Frames[1], RawInputModifiers.Shift);

        AnimationEditor.Views.Controls.FlankerNumericField length = editor.Control<AnimationEditor.Views.Controls.FlankerNumericField>("PropFrameLen");
        length.Value.ShouldBeNull("two different lengths show as mixed");
        length.PlaceholderText.ShouldBe("(mixed)");
        editor.ClickRow(walk);

        (walk.Frames[0].FrameLength, walk.Frames[1].FrameLength).ShouldBe((0.1f, 0.25f));
        editor.UndoManager.CanUndo.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ColorFields_ChangingModeAndRed_UpdateTheFrame_AndSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        ComboBox mode = editor.Control<ComboBox>("PropColorMode");
        mode.Focus();
        editor.Layout();

        editor.Press(Key.Down);
        editor.Press(Key.Down);
        editor.TypeNumber("PropRed", "128");

        walk.Frames[0].ColorOperation.ShouldBe(ColorOperation.Add);
        walk.Frames[0].Red.ShouldBe(128);
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        saved.AnimationChains.Single().Frames.Single().Red.ShouldBe(128);
        saved.AnimationChains.Single().Frames.Single().ColorOperation.ShouldBe(ColorOperation.Add);
    }

    [AvaloniaFact]
    public async Task CutChain_LeavesItInPlaceAsPending_UntilItIsPastedIntoAnotherDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(enemy);
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));

        // Cut is a pending move, as in a file manager: nothing leaves until the paste lands.
        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Project.AnimationChains.Count.ShouldBe(1);
        editor.Services.PendingCutState.IsActive.ShouldBeTrue();

        editor.ClickTab(editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith("enemy.achx")).DisplayName);
        editor.ClickRow(editor.ChainNamed("Bite"));
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Bite", "Walk" });
        editor.Services.PendingCutState.IsActive.ShouldBeFalse();
        editor.ClickTab(editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith("hero.achx")).DisplayName);
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.Project.AnimationChains.ShouldBeEmpty("the cut source is gone from its own document");
        AnimationEditorHarness.ReadSaved(hero).AnimationChains.ShouldBeEmpty("and from its file, since edits auto-save");
    }

    [AvaloniaFact]
    public async Task FilteringOutTheSelectedChain_KeepsItVisible_SoDeleteNeverActsOnSomethingUnseen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "ru");

        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk", "Run" });
        editor.RowFor(editor.ChainNamed("Walk")).IsVisible.ShouldBeTrue();

        // Focus is still in the search box after typing, where Delete edits text; the user clicks
        // the row they mean first.
        editor.Press(Key.Delete);
        editor.Project.AnimationChains.Count.ShouldBe(2);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.Delete);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run" });
    }

    [AvaloniaFact]
    public async Task DuplicatedChain_HasItsOwnShapes_NotSharedWithTheOriginal()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave original = walk.Frames[0].ShapesSave!.AARectSaves.Single();
        editor.ClickRow(walk);
        editor.Press(Key.D, RawInputModifiers.Control);
        AnimationChainSave copy = editor.Project.AnimationChains.Single(chain => chain != walk);
        AARectSave copied = copy.Frames[0].ShapesSave!.AARectSaves.Single();
        copied.ShouldNotBeSameAs(original);
        editor.Expand(copy);
        editor.Expand(copy.Frames[0]);
        editor.ClickRow(copied);

        editor.TypeNumber("PropRectX", "9");

        copied.X.ShouldBe(9f);
        original.X.ShouldBe(0f, "editing the copy must not touch the original");
    }

    [AvaloniaFact]
    public async Task HotReloadDuringAnInlineRename_DoesNotCrash_AndEndsConsistentWithDisk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Stroll");

        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        (await editor.WaitUntilAsync(() => editor.Project.AnimationChains.Count == 2, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        editor.Press(Key.Enter);
        editor.Wait(TimeSpan.FromMilliseconds(300));

        editor.ThrowIfErrorShown();
        AnimationChainListSave onDisk = AnimationEditorHarness.ReadSaved(path);
        onDisk.AnimationChains.Select(chain => chain.Name).ShouldBe(editor.Project.AnimationChains.Select(chain => chain.Name), "memory and disk agree after the race");
        editor.Nodes.Any(node => node.IsEditing).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task HotReload_AfterAnEdit_LeavesUndoInAStateThatDoesNotCorruptTheProject()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));

        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        (await editor.WaitUntilAsync(() => editor.Project.AnimationChains.Any(chain => chain.Name == "Run"), TimeSpan.FromSeconds(5))).ShouldBeTrue();
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(300));

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.ShouldAllBe(chain => chain.Frames.All(frame => float.IsFinite(frame.LeftCoordinate)));
        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(editor.Project.AnimationChains.Select(chain => chain.Name), "the tree matches the model after undo across a reload");
    }

    [AvaloniaFact]
    public async Task RenamingAVisibleChain_ToSomethingTheFilterExcludes_HidesItOnceDeselected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "wa");
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Jump");
        editor.Press(Key.Enter);

        walk.Name.ShouldBe("Jump");
        editor.VisibleChainHeaders.ShouldBe(new[] { "Jump" });

        // The filter is not re-applied by a rename, only by the next change to the box.
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "ru");

        editor.Nodes.First(node => node.Header == "Run").PinnedVisible.ShouldBeTrue();
        editor.Nodes.First(node => node.Header == "Jump").PinnedVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task SixtyAddFrameClicks_AllLand_AndTheTreeKeepsUp()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 60; i++)
        {
            editor.Click(editor.RowButton(walk, "Add Frame"));
        }

        walk.Frames.Count.ShouldBe(61);
        editor.Nodes.Count(node => node.IsFrameNode).ShouldBe(61);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
        editor.ThrowIfErrorShown();
        editor.Press(Key.S, RawInputModifiers.Control);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(61);
    }

    [AvaloniaFact]
    public async Task SpaceWithTheLoopToggleFocused_TogglesTheButton_NotPlayback()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        bool playingBefore = editor.Preview.IsPlaying;
        ToggleButton loop = editor.Control<ToggleButton>("LoopToggle");
        editor.Click(loop);
        bool loopAfterClick = loop.IsChecked == true;
        loop.Focus();
        editor.Layout();

        editor.Press(Key.Space);

        editor.Preview.IsPlaying.ShouldBe(playingBefore, "Space on a focused button is the button's, not the playback hotkey");
        (loop.IsChecked == true).ShouldBe(!loopAfterClick);
    }

    [AvaloniaFact]
    public async Task VeryLongChainName_RoundTripsThroughSaveAndReload()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        string longName = new string('W', 300);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type(longName);
        editor.Press(Key.Enter);

        walk.Name.ShouldBe(longName);
        editor.ClickMenu("MenuReloadFromDisk");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Project.AnimationChains.Single().Name.ShouldBe(longName);
        editor.ThrowIfErrorShown();
    }
}
