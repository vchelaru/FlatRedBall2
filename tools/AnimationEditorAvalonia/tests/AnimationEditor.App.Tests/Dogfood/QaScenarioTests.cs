using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Exploratory QA: actions in odd orders, hotkeys with focus in the wrong place, junk typed into
/// fields, the same key hammered, and edits while something else (playback, a rename, a drag) is
/// still going on. Each scenario states what a user would expect and lets the editor disagree.
/// </summary>
public class QaScenarioTests
{
    [AvaloniaFact]
    public async Task CtrlNTwice_GivesTwoUntitledTabsWithDifferentNames()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));

        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.Press(Key.N, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Tabs.Tabs.Count.ShouldBe(3);
        editor.TabLabels.Distinct().Count().ShouldBe(3, "every tab needs its own label");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task CtrlZ_WithNothingToUndo_AndCtrlY_WithNothingToRedo_DoNothingQuietly()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));

        for (int i = 0; i < 5; i++)
        {
            editor.Press(Key.Z, RawInputModifiers.Control);
            editor.Press(Key.Y, RawInputModifiers.Control);
        }

        editor.Project.AnimationChains.Single().Frames.Count.ShouldBe(1);
        editor.ThrowIfErrorShown();
        editor.ToastText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task DeleteKey_WhileTypingInTheInspector_EditsTheTextNotTheFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        TextBox box = editor.Control<NumericUpDown>("PropPixelX").GetVisualDescendants().OfType<TextBox>().First();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type("8");

        editor.Press(Key.Delete);

        walk.Frames.Count.ShouldBe(1, "Delete inside a text box must not delete the selected frame");
        editor.Press(Key.Enter);
        walk.Frames[0].LeftCoordinate.ShouldBe(8f / 64f);
    }

    [AvaloniaFact]
    public async Task DeleteKey_TwiceInARow_DeletesTwoChains_AndUndoTwiceRestoresBoth()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));

        editor.Press(Key.Delete);
        editor.Press(Key.Delete);

        editor.Project.AnimationChains.Count.ShouldBeLessThanOrEqualTo(2);
        editor.ThrowIfErrorShown();
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run", "Jump" });
    }

    [AvaloniaFact]
    public async Task DeletingTheChainThatIsPlaying_StopsThePreview_WithoutAnError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        if (!editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.Press(Key.Delete);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        editor.Project.AnimationChains.ShouldBeEmpty();
        editor.Preview.Playback.Chain.ShouldBeNull("the preview must not keep playing a deleted chain");
        editor.ThrowIfErrorShown();
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task DuplicatingAChainThreeTimes_GivesThreeDistinctNames()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        for (int i = 0; i < 3; i++)
        {
            editor.ClickRow(walk);
            editor.Press(Key.D, RawInputModifiers.Control);
        }

        editor.Project.AnimationChains.Count.ShouldBe(4);
        editor.Project.AnimationChains.Select(chain => chain.Name).Distinct().Count().ShouldBe(4);
        editor.VisibleChainHeaders.Count.ShouldBe(4);
    }

    [AvaloniaFact]
    public async Task EscapeDuringAHandleDrag_LeavesTheFrameWhereItWas()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 128, 128);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (16, 16, 32, 32)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationFrameSave frame = walk.Frames[0];
        editor.Expand(walk);
        editor.ClickRow(frame);
        Avalonia.Rect box = editor.WireframeRectOf(frame);
        Avalonia.Point corner = new Avalonia.Point(box.Right, box.Bottom);
        Avalonia.Point target = editor.WireframePointAt(80, 80);

        editor.Window.MouseMove(corner, RawInputModifiers.None);
        editor.Window.MouseDown(corner, MouseButton.Left, RawInputModifiers.None);
        editor.Window.MouseMove(target, RawInputModifiers.LeftMouseButton);
        editor.Press(Key.Escape);
        editor.Window.MouseUp(target, MouseButton.Left, RawInputModifiers.None);
        editor.Layout();

        // Either the drag was cancelled (frame back where it started, nothing to undo) or it
        // completed as one undoable step; a half-applied edit with no undo entry is the failure.
        (int x, int y, int width, int height) = editor.PixelRectOf(frame);
        if ((x, y, width, height) != (16, 16, 32, 32))
        {
            editor.UndoManager.CanUndo.ShouldBeTrue("a drag that changed the frame must be undoable");
            editor.Press(Key.Z, RawInputModifiers.Control);
            editor.PixelRectOf(frame).ShouldBe((16, 16, 32, 32));
        }
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task FrameLength_TypingJunk_LeavesTheValueAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        foreach (string junk in new[] { "abc", "0.5.5", "1e3x", "" })
        {
            editor.TypeFlanker("PropFrameLen", junk);
        }

        walk.Frames[0].FrameLength.ShouldBe(0.1f);
        editor.UndoManager.CanUndo.ShouldBeFalse("junk must not record undo entries");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task GridSize_TypingZero_DoesNotBreakTheWireframe()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Click(editor.Control<ToggleButton>("SnapToGridCheck"));

        editor.TypeFlanker("GridSizeInput", "0");
        editor.ClickAt(editor.WireframePointAt(40, 40), RawInputModifiers.Control);

        editor.ThrowIfErrorShown();
        foreach (AnimationFrameSave frame in walk.Frames)
        {
            (frame.RightCoordinate - frame.LeftCoordinate).ShouldBeGreaterThan(0f);
            float.IsFinite(frame.LeftCoordinate).ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public async Task OpeningTheSameFileTwice_FocusesTheExistingTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));

        await editor.OpenAsync(path);

        editor.Tabs.Tabs.Count.ShouldBe(1);
        editor.ChainNamed("Walk").Frames.Count.ShouldBe(2, "re-opening must not throw away the edit");
        editor.UndoManager.CanUndo.ShouldBeTrue("nor the undo history");
    }

    [AvaloniaFact]
    public async Task PasteWithAnEmptyClipboard_DoesNothing_AndShowsNoError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        await editor.Window.Clipboard!.ClearAsync();

        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Project.AnimationChains.Count.ShouldBe(1);
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task RenamingToTheSameNameWithTrailingSpace_ChangesNothing_AndRecordsNoUndo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Walk ");
        editor.Press(Key.Enter);

        walk.Name.ShouldBe("Walk");
        editor.UndoManager.CanUndo.ShouldBeFalse();
        editor.ErrorBannerText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task RenamingWhilePlaying_KeepsPlaying_AndTheNewNameShows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        if (!editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }

        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Stroll");
        editor.Press(Key.Enter);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        walk.Name.ShouldBe("Stroll");
        editor.Preview.IsPlaying.ShouldBeTrue();
        editor.Preview.Playback.Chain.ShouldBeSameAs(walk);
        editor.VisibleChainHeaders.ShouldBe(new[] { "Stroll" });
    }

    [AvaloniaFact]
    public async Task RowAddFrameButton_OnAnUnselectedChain_AddsToThatChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.ClickRow(walk);

        editor.Click(editor.RowButton(run, "Add Frame"));

        run.Frames.Count.ShouldBe(2);
        walk.Frames.Count.ShouldBe(1);
        editor.Services.SelectedState.SelectedFrame.ShouldBeSameAs(run.Frames[1]);
        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(run);
    }

    [AvaloniaFact]
    public async Task SpaceInTheSearchBox_TypesASpace_InsteadOfTogglingPlayback()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        bool playingBefore = editor.Preview.IsPlaying;
        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        TextBox box = editor.Control<TextBox>("SearchBox");
        box.Focus();
        editor.Layout();

        // A real space key raises both the key event (the playback hotkey's path) and the text.
        editor.Type("Wa");
        editor.Press(Key.Space);
        editor.Type(" ");
        editor.Type("l");

        editor.Preview.IsPlaying.ShouldBe(playingBefore);
        box.Text.ShouldBe("Wa l");
    }

    [AvaloniaFact]
    public async Task SwitchingTabsTenTimes_KeepsEachTabsSelectionAndUndoStack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));
        await editor.OpenAsync(enemy);
        editor.ClickRow(editor.ChainNamed("Bite"));
        string heroTab = editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith("hero.achx")).DisplayName;
        string enemyTab = editor.Tabs.ActiveTab!.DisplayName;

        for (int i = 0; i < 5; i++)
        {
            editor.ClickTab(heroTab);
            editor.ClickTab(enemyTab);
        }

        editor.ThrowIfErrorShown();
        editor.UndoManager.CanUndo.ShouldBeFalse("enemy.achx has no edits; hero's undo stack must not leak into it");
        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe("Bite");
        editor.ClickTab(heroTab);
        editor.UndoManager.CanUndo.ShouldBeTrue("hero.achx still has its Add Frame to undo");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.ChainNamed("Walk").Frames.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task UndoInTheOtherTab_DoesNotUndoThisTabsEdit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string enemy = editor.WriteAchx("enemy.achx", AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));
        await editor.OpenAsync(enemy);

        editor.Press(Key.Z, RawInputModifiers.Control);

        AnimationEditorHarness.ReadSaved(hero).AnimationChains.Single().Frames.Count.ShouldBe(2, "hero's edit must survive an undo pressed in enemy's tab");
        editor.ChainNamed("Bite").Frames.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task WheelZoomTwentyNotchesEachWay_StaysFiniteAndClamped()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        Avalonia.Point point = editor.WireframePointAt(32, 32);

        for (int i = 0; i < 20; i++)
        {
            editor.Wheel(point, 1, RawInputModifiers.Control);
        }
        await editor.WaitUntilAsync(() => !editor.Wireframe.IsZoomAnimating, TimeSpan.FromSeconds(3));
        float zoomedIn = editor.Wireframe.CameraState.Zoom;
        for (int i = 0; i < 20; i++)
        {
            editor.Wheel(point, -1, RawInputModifiers.Control);
        }
        await editor.WaitUntilAsync(() => !editor.Wireframe.IsZoomAnimating, TimeSpan.FromSeconds(3));
        float zoomedOut = editor.Wireframe.CameraState.Zoom;

        float.IsFinite(zoomedIn).ShouldBeTrue();
        float.IsFinite(zoomedOut).ShouldBeTrue();
        zoomedIn.ShouldBeGreaterThan(zoomedOut);
        zoomedOut.ShouldBeGreaterThan(0f);
        editor.Control<AnimationEditor.App.Controls.ZoomControl>("WireframeZoom").Text.ShouldEndWith("%");
    }

    [AvaloniaFact]
    public async Task AltUp_OnTheFirstChain_DoesNothing_AndRecordsNoUndo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));

        editor.Press(Key.Up, RawInputModifiers.Alt);
        editor.Press(Key.Up, RawInputModifiers.Alt);

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
        editor.UndoManager.CanUndo.ShouldBeFalse("a move that went nowhere is not an edit");
    }

    [AvaloniaFact]
    public async Task EmptyChain_PlaysScrubsAndTakesAFrame_WithoutAnError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Idle", "sheet.png"));
        await editor.OpenAsync(path);
        AnimationChainSave idle = editor.ChainNamed("Idle");
        editor.ClickRow(idle);

        editor.Press(Key.Space);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        Border surface = editor.Control<Border>("TimelineScrubSurface");
        if (surface.IsEffectivelyVisible && surface.Bounds.Width > 0)
        {
            editor.ClickAt(editor.PointIn(surface, surface.Bounds.Width / 2, surface.Bounds.Height / 2));
        }
        editor.Click(editor.RowButton(idle, "Add Frame"));

        editor.ThrowIfErrorShown();
        idle.Frames.Count.ShouldBe(1);
        // The frame arrives with no texture; the wireframe borrows the project's first texture so
        // a Ctrl+click can seed it (#618). Seeding the frame itself is a README finding.
    }

    [AvaloniaFact]
    public async Task LockedChain_HidesAddFrame_AndRefusesInspectorEdits()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Click(editor.RowButton(walk, "Lock Animation"));
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.RowButton(walk, "Add Frame").IsEffectivelyVisible.ShouldBeFalse("a locked chain cannot take frames");
        editor.TypeNumber("PropPixelX", "8");
        editor.TypeFlanker("PropFrameLen", "0.5");

        walk.Frames[0].LeftCoordinate.ShouldBe(0f);
        walk.Frames[0].FrameLength.ShouldBe(0.1f);
        editor.RightClickRow(walk);
        editor.TreeMenuHeaders.ShouldNotContain("Add Frame");
    }

    [AvaloniaFact]
    public async Task OpeningACorruptAchx_TellsTheUser_AndLeavesTheEditorUsable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string good = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string bad = Path.Combine(editor.ProjectFolder, "broken.achx");
        File.WriteAllText(bad, "<AnimationChainArrayS");
        await editor.OpenAsync(good);

        await editor.OpenAsync(bad);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ErrorBannerText.ShouldNotBeNull("a file that cannot be read must be reported");
        editor.ErrorBannerText!.ShouldContain("broken.achx");
        editor.ClickTab(editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith("hero.achx")).DisplayName);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));
        editor.ChainNamed("Walk").Frames.Count.ShouldBe(2, "the editor still works afterwards");
    }

    [AvaloniaFact]
    public async Task OpeningAnAchxWhosePngIsMissing_TakesEdits_ButRefusesToSaveAndSaysWhy()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "gone.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.ClickRow(walk);
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Click(editor.RowButton(walk, "Add Frame"));
        editor.Press(Key.S, RawInputModifiers.Control);

        walk.Frames.Count.ShouldBe(2);
        editor.Wireframe.BitmapSize.Width.ShouldBe(0, "nothing to draw");
        // A pixel-coordinate file cannot be written without the texture's size, so the editor
        // refuses the save and says so, rather than writing a file with made-up coordinates.
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");
        editor.ToastText.ShouldNotBeNull();
        editor.ToastText!.ShouldContain("gone.png");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(1, "the file on disk is left as it was");
    }

    [AvaloniaFact]
    public async Task PastingFramesIntoALockedChain_IsRefused()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Press(Key.C, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ClickRow(run);
        editor.Click(editor.RowButton(run, "Lock Animation"));

        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        run.Frames.Count.ShouldBe(1, "a locked chain must not accept pasted frames");
    }

    [AvaloniaFact]
    public async Task SaveAs_KeepsTheUndoHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Click(editor.RowButton(walk, "Add Frame"));
        string copy = Path.Combine(editor.ProjectFolder, "hero-copy.achx");

        editor.Dialogs.AnswerNextSaveFile(copy);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.UndoManager.CanUndo.ShouldBeTrue("Save As is not an edit boundary");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.ChainNamed("Walk").Frames.Count.ShouldBe(1);
        AnimationEditorHarness.ReadSaved(copy).AnimationChains.Single().Frames.Count.ShouldBe(1, "and the undo auto-saves to the new file");
    }

    [AvaloniaFact]
    public async Task TabKey_FromPixelX_MovesToPixelY_AndTypingLandsThere()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        TextBox pixelX = editor.Control<NumericUpDown>("PropPixelX").GetVisualDescendants().OfType<TextBox>().First();
        pixelX.Focus();
        editor.Layout();

        editor.Press(Key.Tab);
        editor.Layout();
        TextBox pixelY = editor.Control<NumericUpDown>("PropPixelY").GetVisualDescendants().OfType<TextBox>().First();
        pixelY.IsFocused.ShouldBeTrue("Tab should move to the next field");
        pixelY.SelectAll();
        editor.Type("24");
        editor.Press(Key.Enter);

        walk.Frames[0].TopCoordinate.ShouldBe(24f / 64f);
        walk.Frames[0].LeftCoordinate.ShouldBe(0f);
    }

    [AvaloniaFact]
    public async Task TinyWindow_StillTakesEdits()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Window.Width = 400;
        editor.Window.Height = 300;
        editor.Layout();

        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);
        editor.Press(Key.D, RawInputModifiers.Control);

        editor.Project.AnimationChains.Count.ShouldBe(2);
        editor.ThrowIfErrorShown();
    }
}
