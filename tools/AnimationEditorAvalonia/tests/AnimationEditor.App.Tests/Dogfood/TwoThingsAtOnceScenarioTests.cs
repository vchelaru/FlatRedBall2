using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Two things happening at once: playback while the file is rewritten on disk, while a frame is
/// deleted, reordered or undone, while the tab changes, while the chain is locked; the search
/// filter while a chain is pasted or the file hot-reloads; a drag interrupted by a hot reload;
/// a burst of edits followed at once by Reload From Disk.
/// </summary>
public class TwoThingsAtOnceScenarioTests
{
    private static readonly TimeSpan HotReloadTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The preview auto-plays once a chain is selected; the button toggles, so only click it when stopped.</summary>
    private static void EnsurePlaying(AnimationEditorHarness editor)
    {
        if (!editor.Preview.IsPlaying)
        {
            editor.Click(editor.Control<Button>("PlayPauseBtn"));
        }
        editor.Preview.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task BurstOfEdits_ThenReloadFromDiskAtOnce_ShowsTheLastEdit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        for (int x = 1; x <= 20; x++)
        {
            editor.TypeNumber("PropPixelX", x.ToString());
        }
        editor.ClickMenu("MenuReloadFromDisk");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ThrowIfErrorShown();
        editor.PixelRectOf(editor.ChainNamed("Walk").Frames[0]).X.ShouldBe(20, "every auto-save landed before the reload read the file");
    }

    [AvaloniaFact]
    public async Task DeletingAFrame_WhilePlaying_KeepsPlayingTheRest_WithoutAnError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk);
        EnsurePlaying(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Preview.IsPlaying.ShouldBeTrue();

        editor.ClickRow(walk.Frames[1]);
        editor.Press(Key.Delete);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));

        editor.ThrowIfErrorShown();
        walk.Frames.Count.ShouldBe(2);
        editor.Nodes.Count(node => node.Data is AnimationFrameSave).ShouldBe(2);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(2);
        editor.Preview.IsPlaying.ShouldBeTrue("deleting a frame is not stopping");
    }

    [AvaloniaFact]
    public async Task DragOfAFrame_InterruptedByAHotReload_EndsWithTreeAndModelInAgreement()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        Rect box = editor.WireframeRectOf(walk.Frames[0]);
        Point from = box.Center;

        editor.Window.MouseMove(from, RawInputModifiers.None);
        editor.Window.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(new Point(from.X + 10, from.Y + 10), RawInputModifiers.LeftMouseButton);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        (await editor.WaitUntilAsync(() => editor.Project.AnimationChains.Count == 2, HotReloadTimeout)).ShouldBeTrue("the file rewrite is picked up mid-drag");
        editor.Window.MouseMove(new Point(from.X + 20, from.Y + 20), RawInputModifiers.LeftMouseButton);
        editor.Window.MouseUp(new Point(from.X + 20, from.Y + 20), MouseButton.Left, RawInputModifiers.None);
        editor.Layout();

        editor.ThrowIfErrorShown();
        editor.Nodes.Count(node => node.IsChainNode).ShouldBe(editor.Project.AnimationChains.Count, "the tree matches the model");
        AnimationChainSave reloadedWalk = editor.ChainNamed("Walk");
        editor.Nodes.Count(node => node.Data is AnimationFrameSave && reloadedWalk.Frames.Contains(node.Data)).ShouldBe(reloadedWalk.Frames.Count);
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        saved.AnimationChains.Count.ShouldBe(2, "whatever the drag did, the file still has both chains");
    }

    [AvaloniaFact]
    public async Task HotReloadWhilePlaying_ShowsTheNewChain_AndKeepsPlaying()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        EnsurePlaying(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Preview.IsPlaying.ShouldBeTrue();

        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
        (await editor.WaitUntilAsync(() => editor.Project.AnimationChains.Count == 2, HotReloadTimeout)).ShouldBeTrue();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));

        editor.ThrowIfErrorShown();
        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Walk", "Run" });
        editor.Services.SelectedState.SelectedChain.ShouldNotBeNull("a chain is still selected after the reload");
        editor.Preview.IsPlaying.ShouldBeTrue("the reload swaps the objects under the preview without stopping it");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task LockingTheChain_WhilePlaying_KeepsPlaying_AndRefusesTheNextEdit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        EnsurePlaying(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Click(editor.RowButton(walk, "Lock Animation"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Preview.IsPlaying.ShouldBeTrue("locking is not stopping");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Press(Key.Delete);
        walk.Frames.Count.ShouldBe(2, "a locked chain keeps its frames");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task ReorderWithAltDown_WhilePlaying_ReordersTheFrames_AndKeepsPlaying()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk);
        EnsurePlaying(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        AnimationFrameSave first = walk.Frames[0];

        editor.ClickRow(first);
        editor.Press(Key.Down, RawInputModifiers.Alt);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));

        editor.ThrowIfErrorShown();
        walk.Frames[1].ShouldBeSameAs(first);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames[1].LeftCoordinate.ShouldBe(0, "the new order is saved");
    }

    [AvaloniaFact]
    public async Task SearchFilterActive_ThenHotReloadAddsChains_EveryReloadedChainShows_UntilTheFilterIsRetyped()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "Ru");

        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        (await editor.WaitUntilAsync(() => editor.Project.AnimationChains.Count == 3, HotReloadTimeout)).ShouldBeTrue();

        // Model changes are grow-only by design: they never hide a row, only typing does.
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk", "Run", "Jump" });
        editor.Control<TextBox>("SearchBox").Text.ShouldBe("Ru", "the filter text survives the reload");
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "R");
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "Ru");
        editor.VisibleChainHeaders.ShouldContain("Run");
        editor.VisibleChainHeaders.ShouldNotContain("Jump", "retyping the filter hides what does not match");
    }

    [AvaloniaFact]
    public async Task SearchFilterActive_ThenPaste_ThePastedChainIsVisibleAndSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "Walk");
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });

        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Count.ShouldBe(3);
        AnimationChainSave pasted = editor.Project.AnimationChains.Single(chain => chain.Name is not ("Walk" or "Run"));
        editor.VisibleChainHeaders.ShouldContain(pasted.Name, "a model change never hides a row, so the paste is seen");
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(pasted, "and the paste selects what it added");
    }

    [AvaloniaFact]
    public async Task SwitchingTabs_WhilePlaying_ShowsTheOtherDocument_WithoutAnError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(enemy);
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        EnsurePlaying(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.ClickTab("enemy.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Bite" });
        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe("Bite", "the preview and selection belong to the tab that is showing");
        editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.ThrowIfErrorShown();
        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe("Walk");
    }

    [AvaloniaFact]
    public async Task UndoWhilePlaying_RestoresTheDeletedFrame_AndPinsItAsTheSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[2]);
        editor.Press(Key.Delete);
        walk.Frames.Count.ShouldBe(2);
        editor.ClickRow(walk);
        EnsurePlaying(editor);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Press(Key.Z, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));

        editor.ThrowIfErrorShown();
        walk.Frames.Count.ShouldBe(3);
        editor.Nodes.Count(node => node.Data is AnimationFrameSave).ShouldBe(3);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(walk.Frames[2], "undo puts the selection back on the restored frame");
        editor.Preview.IsPlaying.ShouldBeFalse("and a selected frame pins the preview on it, as a click on the frame would");
    }
}
