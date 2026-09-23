using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Every control no scenario had driven yet: the Edit and View menus item by item, the PixiJS
/// export, Associate Tiled Tileset, Follow System theme, Expand All / Collapse All, the preview
/// checkboxes, the PNG usage overlay, the Move / Magic Wand pair, the
/// remaining inspector fields, File > New and File > Save through the menu, crash recovery on
/// the next start, and the open note about an empty chain's first frame.
/// </summary>
public class SweepScenarioTests
{
    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="sheet.png" width="64" height="64"/>
        </tileset>
        """;

    [AvaloniaFact]
    public async Task AssociateTiledTileset_ThroughTheMenu_RecordsTheAssociation_AndTheNextEditStillSaves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string tsx = Path.Combine(editor.ProjectFolder, "Heroes.tsx");
        File.WriteAllText(tsx, TsxXml);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Dialogs.AnswerNextOpenFile(tsx);

        editor.ClickMenu("MenuAssociateTiledTileset");
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.ThrowIfErrorShown();
        editor.Services.IoManager.GetAssociatedTiledTilesetPaths(path).ShouldContain(tsxPath => tsxPath.EndsWith("Heroes.tsx"));
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.ThrowIfErrorShown();
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task CrashRecovery_UnsavedUntitledEdits_ComeBackOnTheNextStart_AndDismissHidesTheBanner()
    {
        string settingsRoot = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string recoveryCopy = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N") + ".achx");
        using (AnimationEditorHarness first = new AnimationEditorHarness(settingsRoot))
        {
            first.Dialogs.AnswerNextSaveFile(null);
            first.Press(Key.N, RawInputModifiers.Control);
            first.Wait(TimeSpan.FromMilliseconds(100));
            first.Click(first.Control<Button>("AddChainBtn"));
            first.Wait(TimeSpan.FromMilliseconds(50));
            first.Type("Recovered");
            first.Press(Key.Enter);
            first.Wait(TimeSpan.FromMilliseconds(100));
            File.Exists(first.Services.IoManager.RecoveryFilePath).ShouldBeTrue("an unsaved document writes a recovery file on every edit");
            // A crash leaves the file where it is; copy it aside so disposing this editor cannot tidy it.
            File.Copy(first.Services.IoManager.RecoveryFilePath, recoveryCopy);
        }

        using AnimationEditorHarness restarted = new AnimationEditorHarness(settingsRoot, recoveryCopy);
        restarted.Wait(TimeSpan.FromMilliseconds(200));

        restarted.Control<Control>("RecoveredDocumentBanner").IsVisible.ShouldBeTrue("the user is told a document was recovered");
        restarted.Project.AnimationChains.Select(chain => chain.Name).ShouldContain("Recovered");
        restarted.TabLabels.ShouldContain(label => label.StartsWith("Untitled"), "the recovered document is an untitled tab");
        restarted.Click(restarted.Control<Button>("DismissRecoveredDocumentBtn"));
        restarted.Control<Control>("RecoveredDocumentBanner").IsVisible.ShouldBeFalse();
        File.Exists(recoveryCopy).ShouldBeFalse("the recovery file is consumed once restored");
    }

    [AvaloniaFact]
    public async Task EditMenu_CopyAndPaste_ThenDuplicate_AddChains_AndUndoTakesThemBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));

        editor.ClickMenu("MenuCopy");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.ClickMenu("MenuPaste");
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Count.ShouldBe(2, "Edit > Paste adds the copied chain");
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.ClickMenu("MenuDuplicate");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ThrowIfErrorShown();
        editor.Project.AnimationChains.Count.ShouldBe(3, "Edit > Duplicate adds another");
        editor.Project.AnimationChains.Select(chain => chain.Name).Distinct().Count().ShouldBe(3, "each copy gets its own name");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task EditMenu_UndoAndRedoItems_EnableWithTheStack_AndDoWhatTheHotkeysDo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Control<MenuItem>("MenuUndo").IsEnabled.ShouldBeFalse("nothing to undo yet");
        editor.Control<MenuItem>("MenuRedo").IsEnabled.ShouldBeFalse();

        editor.Click(editor.RowButton(walk, "Add Frame"));
        editor.Control<MenuItem>("MenuUndo").IsEnabled.ShouldBeTrue();
        editor.ClickMenu("MenuUndo");

        walk.Frames.Count.ShouldBe(1);
        editor.Control<MenuItem>("MenuUndo").IsEnabled.ShouldBeFalse();
        editor.Control<MenuItem>("MenuRedo").IsEnabled.ShouldBeTrue();
        editor.ClickMenu("MenuRedo");
        walk.Frames.Count.ShouldBe(2);
        editor.Control<MenuItem>("MenuRedo").IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task EmptyChainBesideATexturedOne_AddFrame_UsesTheTextureTheCanvasIsShowing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave idle = new AnimationChainSave { Name = "Idle" };
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)), idle);
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));
        editor.ClickRow(editor.ChainNamed("Idle"));

        editor.Click(editor.RowButton(editor.ChainNamed("Idle"), "Add Frame"));

        AnimationFrameSave added = editor.ChainNamed("Idle").Frames.Single();
        added.TextureName.ShouldBe("sheet.png", "the only texture in the project, and the one the canvas was just showing, is the obvious choice");
        editor.Wireframe.BitmapSize.ShouldBe((64, 64), "and the new frame shows on it");
    }

    [AvaloniaFact]
    public async Task ExpandAllAndCollapseAll_Buttons_OpenAndCloseEveryChain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Jump", "sheet.png", (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Nodes.Where(node => node.IsChainNode).ShouldAllBe(node => !node.IsExpanded);

        editor.Click(editor.Control<Button>("ExpandAllBtn"));
        editor.Nodes.Where(node => node.IsChainNode).ShouldAllBe(node => node.IsExpanded);
        editor.Nodes.Count(node => node.Data is AnimationFrameSave).ShouldBe(3, "every frame row is now in the tree");
        editor.Click(editor.Control<Button>("CollapseAllBtn"));

        editor.Nodes.Where(node => node.IsChainNode).ShouldAllBe(node => !node.IsExpanded);
    }

    [AvaloniaFact]
    public async Task ExportToPixiJs_WritesTheJsonAndCopiesTheTexture_AndToasts()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        string exportDir = Path.Combine(editor.ProjectFolder, "export");
        Directory.CreateDirectory(exportDir);
        string json = Path.Combine(exportDir, "hero.json");
        editor.Dialogs.AnswerNextSaveFile(json);

        editor.ClickMenu("MenuExportPixiJs");
        editor.Wait(TimeSpan.FromMilliseconds(300));

        editor.ThrowIfErrorShown();
        File.Exists(json).ShouldBeTrue("the spritesheet JSON is written where the dialog said");
        File.ReadAllText(json).ShouldContain("\"frames\"");
        File.ReadAllText(json).ShouldContain("Walk");
        File.Exists(Path.Combine(exportDir, "sheet.png")).ShouldBeTrue("the texture travels with the JSON");
        editor.ToastText.ShouldNotBeNull();
        editor.ToastText!.ShouldContain("hero.json");
    }

    [AvaloniaFact]
    public async Task FileMenu_NewThenSave_OpenAnUntitledTab_AndAskWhereToSaveIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Dialogs.AnswerNextSaveFile(null);

        editor.ClickMenu("MenuNew");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.TabLabels.ShouldContain(label => label.StartsWith("Untitled"));
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Idle");
        editor.Press(Key.Enter);
        string target = Path.Combine(editor.ProjectFolder, "fresh.achx");
        editor.Dialogs.AnswerNextSaveFile(target);
        editor.ClickMenu("MenuSave");
        editor.Wait(TimeSpan.FromMilliseconds(200));

        editor.ThrowIfErrorShown();
        File.Exists(target).ShouldBeTrue("File > Save on an untitled document asks for a path and writes there");
        AnimationEditorHarness.ReadSaved(target).AnimationChains.Single().Name.ShouldBe("Idle");
        editor.TabLabels.ShouldContain("fresh.achx");
    }

    [AvaloniaFact]
    public async Task FlipVerticalAndDiagonalToggles_FlipTheFrame_AndSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.Click(editor.Control<ToggleButton>("PropFlipV"));
        editor.Click(editor.Control<ToggleButton>("PropFlipD"));

        walk.Frames[0].FlipVertical.ShouldBeTrue();
        walk.Frames[0].FlipDiagonal.ShouldBeTrue();
        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single();
        (saved.FlipVertical, saved.FlipDiagonal).ShouldBe((true, true));
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames[0].FlipDiagonal.ShouldBeFalse("each toggle is its own undo step");
        walk.Frames[0].FlipVertical.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task GreenBlueAndAlphaFields_ReachTheFrame_AndSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.TypeNumber("PropGreen", "64");
        editor.TypeNumber("PropBlue", "32");
        editor.TypeNumber("PropAlpha", "128");

        (walk.Frames[0].Green, walk.Frames[0].Blue, walk.Frames[0].Alpha).ShouldBe((64, 32, 128));
        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single();
        (saved.Green, saved.Blue, saved.Alpha).ShouldBe((64, 32, 128));
    }

    [AvaloniaFact]
    public async Task MoveModeAndMagicWand_AreOneOrTheOther_AndMoveCannotBeTurnedOff()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        ToggleButton move = editor.Control<ToggleButton>("MoveModeToggle");
        ToggleButton wand = editor.Control<ToggleButton>("MagicWandToggle");
        move.IsChecked.ShouldBe(true);

        editor.Click(wand);
        (move.IsChecked, wand.IsChecked).ShouldBe((false, true));
        editor.Wireframe.IsMagicWandMode.ShouldBeTrue();
        editor.Click(move);
        (move.IsChecked, wand.IsChecked).ShouldBe((true, false));
        editor.Click(move);

        (move.IsChecked, wand.IsChecked).ShouldBe((true, false), "clicking Move again leaves it on; there is no no-mode state");
        editor.Wireframe.IsMagicWandMode.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task PngTab_UsageOverlay_FindsTheChainsThatUseThePng()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string png = editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Window.OpenPngAsTab(png);
        await editor.Window.WhenPngTabLoaded();
        editor.Layout();

        editor.Click(editor.Control<ToggleButton>("PngUsageOverlayToggle"));
        PngPreviewControl pane = editor.Control<PngPreviewControl>("PngPane");
        (await editor.WaitUntilAsync(() => pane.UsageRegions.Count > 0, TimeSpan.FromSeconds(5))).ShouldBeTrue("the scan finds the chain on this PNG");

        pane.UsageRegions.Single().Chain.Name.ShouldBe("Walk");
        pane.UsageRegions.Single().Rects.Count.ShouldBe(2, "one region per frame");
        editor.Click(editor.Control<ToggleButton>("PngUsageOverlayToggle"));
        editor.Layout();
        pane.UsageRegions.ShouldBeEmpty("toggling off clears the overlay");
    }

    [AvaloniaFact]
    public async Task PreviewToggles_BoundingBoxDrivesThePreview_AndTheGuidesToggleWaitsForAGuide()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        bool boxBefore = editor.Preview.ShowBoundingBox;

        editor.Click(editor.Control<ToggleButton>("ShowBoundingBoxCheck"));

        editor.Preview.ShowBoundingBox.ShouldBe(!boxBefore);
        editor.Control<ToggleButton>("ShowUserGuidesCheck").IsVisible.ShouldBeFalse("with no guide there is nothing to hide, so the toggle stays out of the toolbar");
    }

    [AvaloniaFact]
    public async Task RemainingInspectorFields_RelY_PixelH_RectYScaleY_CircleXY_AllCommitAndSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.TypeNumber("PropRelY", "5");
        editor.TypeNumber("PropPixelH", "24");
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = walk.Frames[0].ShapesSave!.Shapes.OfType<AARectSave>().Single();
        editor.TypeNumber("PropRectY", "3");
        editor.TypeNumber("PropRectScaleY", "7");
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add Circle");
        CircleSave circle = walk.Frames[0].ShapesSave!.Shapes.OfType<CircleSave>().Single();
        editor.TypeNumber("PropCircleX", "2");
        editor.TypeNumber("PropCircleY", "-4");

        editor.ThrowIfErrorShown();
        walk.Frames[0].RelativeY.ShouldBe(5);
        editor.PixelRectOf(walk.Frames[0]).Height.ShouldBe(24);
        (rect.Y, rect.ScaleY).ShouldBe((3f, 7f));
        (circle.X, circle.Y).ShouldBe((2f, -4f));
        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single();
        saved.RelativeY.ShouldBe(5);
        saved.BottomCoordinate.ShouldBe(24);
        saved.ShapesSave!.Shapes.OfType<AARectSave>().Single().ScaleY.ShouldBe(7);
        saved.ShapesSave.Shapes.OfType<CircleSave>().Single().Y.ShouldBe(-4);
    }

    [AvaloniaFact]
    public async Task ShapeNameFields_RenameTheRectangleAndTheCircle()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        AARectSave rect = walk.Frames[0].ShapesSave!.Shapes.OfType<AARectSave>().Single();
        editor.TypeText("PropRectName", "Hitbox");
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add Circle");
        CircleSave circle = walk.Frames[0].ShapesSave!.Shapes.OfType<CircleSave>().Single();
        editor.TypeText("PropCircleName", "Hurt");

        rect.Name.ShouldBe("Hitbox");
        circle.Name.ShouldBe("Hurt");
        editor.NodeFor(rect).Header.ShouldContain("Hitbox", Case.Sensitive, "the tree row shows the new name");
        editor.NodeFor(circle).Header.ShouldContain("Hurt");
        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single();
        saved.ShapesSave!.Shapes.OfType<AARectSave>().Single().Name.ShouldBe("Hitbox");
        saved.ShapesSave.Shapes.OfType<CircleSave>().Single().Name.ShouldBe("Hurt");
    }

    [AvaloniaFact]
    public async Task ThemeFollowSystem_IsChecked_AndSurvivesARestart()
    {
        string settingsRoot = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        using (AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot))
        {
            editor.ClickMenu("MenuThemeDark");
            editor.ClickMenu("MenuThemeSystem");
            editor.Control<MenuItem>("MenuThemeSystem").IsChecked.ShouldBeTrue();
            editor.Control<MenuItem>("MenuThemeDark").IsChecked.ShouldBeFalse();
        }

        using AnimationEditorHarness restarted = new AnimationEditorHarness(settingsRoot);

        restarted.Control<MenuItem>("MenuThemeSystem").IsChecked.ShouldBeTrue("Follow System is remembered like the other themes");
    }

    [AvaloniaFact]
    public async Task ViewMenu_ShowHistory_SelectsTheHistoryTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));

        editor.ClickMenu("MenuShowHistory");

        editor.Control<TabControl>("SidebarTabs").SelectedItem.ShouldBeSameAs(editor.Control<Control>("HistoryTab"));
        editor.HistoryRows.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task ViewMenu_ZoomItems_StepTheWireframeAndThePreview()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        float wireframeBefore = editor.Wireframe.CameraState.Item3;
        float previewBefore = editor.Preview.Zoom;

        editor.ClickMenu("MenuWireframeZoomIn");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));
        editor.Wireframe.CameraState.Item3.ShouldBeGreaterThan(wireframeBefore);
        editor.ClickMenu("MenuWireframeZoomOut");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));
        editor.Wireframe.CameraState.Item3.ShouldBe(wireframeBefore, tolerance: 0.01f, "one step out undoes one step in");

        editor.ClickMenu("MenuPreviewZoomIn");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));
        editor.Preview.Zoom.ShouldBeGreaterThan(previewBefore);
        editor.ClickMenu("MenuPreviewZoomOut");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));
        editor.Preview.Zoom.ShouldBe(previewBefore, tolerance: 0.01f);
    }
}
