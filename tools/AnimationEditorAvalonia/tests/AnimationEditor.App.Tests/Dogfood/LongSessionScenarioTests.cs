using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Long sessions: mixed edits of every kind across two tabs then undo all the way back in each,
/// fifty chains added and named by keyboard, undo/redo ping-pong, the same file opened and
/// closed twenty times, and a hundred edits spread over tab switches.
/// </summary>
public class LongSessionScenarioTests
{
    [AvaloniaFact]
    public async Task FiftyChainsAddedAndNamedByKeyboard_AllLand_AndReloadFromDiskAgrees()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        List<string> expected = new List<string> { "Walk" };

        for (int i = 0; i < 50; i++)
        {
            editor.Click(editor.Control<Button>("AddChainBtn"));
            editor.Wait(TimeSpan.FromMilliseconds(20));
            editor.Type($"Chain{i:00}");
            editor.Press(Key.Enter);
            expected.Add($"Chain{i:00}");
        }

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(expected);
        editor.ClickMenu("MenuReloadFromDisk");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(expected, "every add and rename was auto-saved");
        editor.Nodes.Count(node => node.IsChainNode).ShouldBe(51);
    }

    [AvaloniaFact]
    public async Task HundredEditsSpreadOverTabSwitches_KeepBothFilesCurrent_AndBothUndoStacksIntact()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 256, 256);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(enemy);
        await editor.OpenAsync(hero);

        for (int round = 1; round <= 25; round++)
        {
            editor.ClickTab("hero.achx");
            AnimationChainSave walk = editor.ChainNamed("Walk");
            editor.Expand(walk);
            editor.ClickRow(walk.Frames[0]);
            editor.TypeNumber("PropPixelX", round.ToString());
            editor.TypeNumber("PropPixelY", round.ToString());
            editor.ClickTab("enemy.achx");
            AnimationChainSave bite = editor.ChainNamed("Bite");
            editor.Expand(bite);
            editor.ClickRow(bite.Frames[0]);
            editor.TypeNumber("PropPixelX", (round * 2).ToString());
            editor.TypeNumber("PropPixelY", (round * 2).ToString());
        }

        editor.ThrowIfErrorShown();
        AnimationFrameSave savedBite = AnimationEditorHarness.ReadSaved(enemy).AnimationChains.Single().Frames.Single();
        (savedBite.LeftCoordinate, savedBite.TopCoordinate).ShouldBe((50f, 50f));
        AnimationFrameSave savedWalk = AnimationEditorHarness.ReadSaved(hero).AnimationChains.Single().Frames.Single();
        (savedWalk.LeftCoordinate, savedWalk.TopCoordinate).ShouldBe((25f, 25f));
        editor.UndoLabels.Count.ShouldBe(50, "the enemy tab holds its own fifty steps");
        editor.ClickTab("hero.achx");
        editor.UndoLabels.Count.ShouldBe(50, "and the hero tab holds its own fifty");
    }

    [AvaloniaFact]
    public async Task MixedEditsOnTwoTabs_ThenUndoAllTheWayBackInEach_RestoresBothFiles()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        byte[] heroBefore = File.ReadAllBytes(hero);
        byte[] enemyBefore = File.ReadAllBytes(enemy);
        await editor.OpenAsync(enemy);
        await editor.OpenAsync(hero);

        // Hero: add a frame, rename, flip, a rectangle, a reorder, a delete, a pixel edit.
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Click(editor.RowButton(walk, "Add Frame"));
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Rename…");
        editor.Dialogs.ThrowIfUnanswered();
        editor.Type("Stroll");
        editor.Press(Key.Enter);
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Flip Vertically");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        editor.ClickRow(editor.ChainNamed("Jump"));
        editor.Press(Key.Up, RawInputModifiers.Alt);
        editor.Press(Key.Delete);
        editor.ClickRow(walk.Frames[1]);
        editor.TypeNumber("PropPixelX", "40");
        editor.ThrowIfErrorShown();
        int heroSteps = editor.UndoLabels.Count;
        heroSteps.ShouldBe(7);

        // Enemy: a pixel edit, a duplicate, a frame length.
        editor.ClickTab("enemy.achx");
        AnimationChainSave bite = editor.ChainNamed("Bite");
        editor.Expand(bite);
        editor.ClickRow(bite.Frames[0]);
        editor.TypeNumber("PropPixelY", "8");
        // Enter leaves focus in the inspector box, where Ctrl+D would edit text; click the row first.
        editor.ClickRow(bite.Frames[0]);
        editor.Press(Key.D, RawInputModifiers.Control);
        editor.ClickRow(bite.Frames[0]);
        editor.TypeFlanker("PropFrameLen", "0.5");
        editor.ThrowIfErrorShown();
        editor.UndoLabels.Count.ShouldBe(3);

        while (editor.UndoManager.CanUndo)
        {
            editor.Press(Key.Z, RawInputModifiers.Control);
        }
        editor.ClickTab("hero.achx");
        editor.UndoLabels.Count.ShouldBe(heroSteps, "the hero tab's stack was untouched by the enemy undos");
        while (editor.UndoManager.CanUndo)
        {
            editor.Press(Key.Z, RawInputModifiers.Control);
        }

        editor.ThrowIfErrorShown();
        File.ReadAllBytes(hero).ShouldBe(heroBefore, "undoing everything writes the hero file back byte for byte");
        File.ReadAllBytes(enemy).ShouldBe(enemyBefore, "and the enemy file");
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Jump" });
        editor.Nodes.Count(node => node.IsChainNode).ShouldBe(2, "the tree followed every undo");
    }

    [AvaloniaFact]
    public async Task OpenAndCloseTheSameFile_TwentyTimes_LeavesNoTab_AndTheEditorStillOpensIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));

        for (int i = 0; i < 20; i++)
        {
            await editor.OpenAsync(path);
            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
            editor.CloseTab("hero.achx");
            editor.TabLabels.ShouldBeEmpty();
        }

        editor.ThrowIfErrorShown();
        await editor.OpenAsync(path);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));
        editor.ChainNamed("Walk").Frames.Count.ShouldBe(2, "the twenty-first open is as good as the first");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task UndoRedoPingPong_TwentyTimes_LeavesModelTreeAndFileWhereTheEditsPutThem()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Click(editor.RowButton(walk, "Add Frame"));
        editor.Click(editor.RowButton(walk, "Add Frame"));
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[2]);
        editor.TypeNumber("PropPixelX", "32");
        editor.RightClickRow(walk);
        editor.PickTreeMenuItem("Invert Frame Order");
        editor.ClickRow(walk.Frames[0]);
        editor.Press(Key.Delete);
        List<int> after = walk.Frames.Select(frame => editor.PixelRectOf(frame).X).ToList();
        after.ShouldBe(new[] { 0, 0 });

        for (int i = 0; i < 20; i++)
        {
            editor.Press(Key.Z, RawInputModifiers.Control);
            editor.Press(Key.Y, RawInputModifiers.Control);
        }

        editor.ThrowIfErrorShown();
        walk.Frames.Select(frame => editor.PixelRectOf(frame).X).ToList().ShouldBe(after);
        editor.Nodes.Count(node => node.Data is AnimationFrameSave).ShouldBe(2);
        editor.UndoLabels.Count.ShouldBe(5);
        editor.UndoManager.CanRedo.ShouldBeFalse();
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(2);
    }
}
