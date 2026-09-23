using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Projects that do not look like the fixtures: textures in subfolders and above the file,
/// backslash paths, non-ASCII and very long folder names, a legacy UV file, duplicate chain
/// names, an empty file, two hundred chains, two hundred frames, a 4096 px texture, a 1 px
/// texture, a non-power-of-two texture, a chain spread over two textures, a read-only file, and
/// a session of a hundred edits undone and redone all the way.
/// </summary>
public class UnusualProjectScenarioTests
{
    [AvaloniaFact]
    public async Task AchxInAVeryDeepFolder_OpensEditsAndSaves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Well past the classic 260-character Windows path limit once the file name is added.
        string deep = editor.ProjectFolder;
        while (deep.Length < 300)
        {
            deep = Path.Combine(deep, "level-" + new string('x', 20));
        }
        Directory.CreateDirectory(deep);
        editor.WritePng(Path.Combine(deep, "sheet.png"), 64, 64);
        string path = editor.WriteAchx(Path.Combine(deep, "hero.achx"), AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));

        await editor.OpenAsync(path);

        editor.ThrowIfErrorShown();
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Wireframe.BitmapSize.ShouldBe((64, 64), "the texture beside a deeply nested file still loads");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.TypeNumber("PropPixelX", "8");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().LeftCoordinate.ShouldBe(8);
    }

    [AvaloniaFact]
    public async Task BackslashTexturePath_Loads_AndSaveNormalizesItToForwardSlashes()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "art"));
        editor.WritePng(Path.Combine("art", "sheet.png"), 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "art\\sheet.png", (0, 0, 16, 16)));

        await editor.OpenAsync(path);

        editor.ThrowIfErrorShown();
        editor.Wireframe.BitmapSize.ShouldBe((64, 64), "a backslash path written by an older tool still resolves");
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.TypeNumber("PropPixelX", "8");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().TextureName
            .ShouldBe("art/sheet.png", "the save heals the separator so the file is portable");
    }

    [AvaloniaFact]
    public async Task ChainWithTwoHundredFrames_ExpandsPlaysAndDeletesInTheMiddle_WithoutAnError()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 256, 256);
        (int, int, int, int)[] frames = Enumerable.Range(0, 200).Select(i => (i % 16 * 16, i / 16 * 16, 16, 16)).ToArray();
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", frames));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Expand(walk);
        editor.ClickRow(walk.Frames[100]);
        editor.Click(editor.Control<Button>("PlayPauseBtn"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));
        editor.Click(editor.Control<Button>("PlayPauseBtn"));
        editor.ClickRow(walk.Frames[100]);
        editor.Press(Key.Delete);

        editor.ThrowIfErrorShown();
        walk.Frames.Count.ShouldBe(199);
        editor.Nodes.Count(node => node.Data is AnimationFrameSave).ShouldBe(199, "the tree follows the delete");
        editor.Press(Key.Z, RawInputModifiers.Control);
        walk.Frames.Count.ShouldBe(200);
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(200);
    }

    [AvaloniaFact]
    public async Task DuplicateChainNames_EditingTheSecond_LeavesTheFirstAlone_AndBothSurviveTheSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        // The .achx format allows two chains with one name and the editor must keep both.
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Walk", "sheet.png", (16, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave first = editor.Project.AnimationChains[0];
        AnimationChainSave second = editor.Project.AnimationChains[1];

        editor.Expand(second);
        editor.ClickRow(second.Frames[0]);
        editor.TypeNumber("PropPixelX", "32");

        editor.PixelRectOf(second.Frames[0]).X.ShouldBe(32);
        editor.PixelRectOf(first.Frames[0]).X.ShouldBe(0, "the edit lands on the clicked chain, not the first chain with that name");
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        saved.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Walk" });
        saved.AnimationChains[1].Frames[0].LeftCoordinate.ShouldBe(32);

        editor.ClickRow(second);
        editor.Press(Key.Delete);
        editor.Project.AnimationChains.Single().ShouldBeSameAs(first, "Delete removes the selected duplicate, not its namesake");
    }

    [AvaloniaFact]
    public async Task EmptyFileWithNoChains_Opens_AndAChainAddedToIt_Saves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("empty.achx");

        await editor.OpenAsync(path);

        editor.ThrowIfErrorShown();
        editor.TabLabels.ShouldBe(new[] { "empty.achx" });
        editor.Nodes.ShouldBeEmpty();
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Idle");
        editor.Press(Key.Enter);
        AnimationChainSave idle = editor.ChainNamed("Idle");
        editor.RightClickRow(idle);
        editor.PickTreeMenuItem("Add Frame");
        idle.Frames.Count.ShouldBe(1);
        editor.Expand(idle);
        editor.ClickRow(idle.Frames[0]);
        editor.TypeText("PropTextureName", "sheet.png");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        saved.AnimationChains.Single().Name.ShouldBe("Idle");
        saved.AnimationChains.Single().Frames.Single().TextureName.ShouldBe("sheet.png");
    }

    [AvaloniaFact]
    public async Task FramesOnTwoTextures_SelectingEachFrame_ShowsItsOwnTexture_AndTheChainStillPlays()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("big.png", 64, 64);
        editor.WritePng("small.png", 32, 32);
        AnimationChainSave fixture = AnimationEditorHarness.Chain("Walk", "big.png", (0, 0, 16, 16));
        fixture.Frames.Add(AnimationEditorHarness.Chain("x", "small.png", (0, 0, 8, 8)).Frames[0]);
        string path = editor.WriteAchx("hero.achx", fixture);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);

        editor.ClickRow(walk.Frames[0]);
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));
        editor.ClickRow(walk.Frames[1]);
        editor.Wireframe.BitmapSize.ShouldBe((32, 32), "the wireframe follows the selected frame's texture");
        editor.PixelRectOf(walk.Frames[1]).ShouldBe((0, 0, 8, 8));

        editor.ClickRow(walk);
        editor.Click(editor.Control<Button>("PlayPauseBtn"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(250));
        editor.Click(editor.Control<Button>("PlayPauseBtn"));
        editor.ThrowIfErrorShown();
        editor.Click(editor.RowButton(walk, "Add Frame"));
        walk.Frames[2].TextureName.ShouldNotBeNullOrEmpty("a new frame borrows a texture from its siblings");
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        saved.AnimationChains.Single().Frames.Select(frame => frame.TextureName).Take(2).ShouldBe(new[] { "big.png", "small.png" });
        saved.AnimationChains.Single().Frames[1].RightCoordinate.ShouldBe(8, "pixel conversion uses each frame's own texture size");
    }

    [AvaloniaFact]
    public async Task HugeTexture_FrameInTheFarCorner_EditsAndSavesInExactPixels()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("atlas.png", 4096, 4096);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "atlas.png", (4000, 4000, 64, 64)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.Wireframe.BitmapSize.ShouldBe((4096, 4096));
        editor.PixelRectOf(walk.Frames[0]).ShouldBe((4000, 4000, 64, 64));
        editor.DoubleClickRow(walk);
        Avalonia.Rect box = editor.WireframeRectOf(walk.Frames[0]);
        Avalonia.Rect panel = new Avalonia.Rect(editor.PointIn(editor.Wireframe, 0, 0), editor.Wireframe.Bounds.Size);
        panel.Contains(box.Center).ShouldBeTrue("fit to view brings the far corner onto the screen");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.TypeNumber("PropPixelX", "4032");

        editor.PixelRectOf(walk.Frames[0]).ShouldBe((4032, 4000, 64, 64));
        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single();
        (saved.LeftCoordinate, saved.RightCoordinate, saved.TopCoordinate, saved.BottomCoordinate).ShouldBe((4032f, 4096f, 4000f, 4064f));
    }

    [AvaloniaFact]
    public async Task HundredEdits_ThenUndoAllTheWayBack_ThenRedoAllTheWayForward_EndsWhereItStarted()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 256, 256);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        for (int x = 1; x <= 100; x++)
        {
            editor.TypeNumber("PropPixelX", x.ToString());
        }
        editor.PixelRectOf(walk.Frames[0]).X.ShouldBe(100);
        editor.UndoLabels.Count.ShouldBe(100, "every edit is its own undo step");

        for (int i = 0; i < 100; i++)
        {
            editor.Press(Key.Z, RawInputModifiers.Control);
        }
        editor.PixelRectOf(walk.Frames[0]).X.ShouldBe(0);
        editor.UndoManager.CanUndo.ShouldBeFalse();
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().LeftCoordinate.ShouldBe(0, "auto-save follows the undos");

        for (int i = 0; i < 100; i++)
        {
            editor.Press(Key.Y, RawInputModifiers.Control);
        }
        editor.PixelRectOf(walk.Frames[0]).X.ShouldBe(100);
        editor.UndoManager.CanRedo.ShouldBeFalse();
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().LeftCoordinate.ShouldBe(100);
    }

    [AvaloniaFact]
    public async Task LegacyUvFile_AcceptingTheConversion_OpensWithTheSameRectangles_AndSavesInPixels()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = WriteUvAchx(editor, "old.achx", ("Walk", 0f, 0f, 0.25f, 0.25f));
        editor.Dialogs.AnswerNextConfirm(true);

        await editor.OpenAsync(path);

        editor.Dialogs.Shown.ShouldContain(text => text.Contains("Convert"), "a UV file asks before it is converted");
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.PixelRectOf(walk.Frames[0]).ShouldBe((0, 0, 16, 16), "0..0.25 of 64 px is 16 px");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.TypeNumber("PropPixelX", "8");
        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        saved.CoordinateType.ShouldBe(TextureCoordinateType.Pixel, "the converted file is written in pixels from then on");
        (saved.AnimationChains.Single().Frames.Single().LeftCoordinate, saved.AnimationChains.Single().Frames.Single().RightCoordinate).ShouldBe((8f, 24f));
    }

    [AvaloniaFact]
    public async Task LegacyUvFile_DecliningTheConversion_OpensNoTab_LeavesTheFileByteForByte_AndKeepsTheCurrentDocument()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Run", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        string path = WriteUvAchx(editor, "old.achx", ("Walk", 0f, 0f, 0.25f, 0.25f));
        byte[] before = File.ReadAllBytes(path);
        editor.Dialogs.AnswerNextConfirm(false);

        await editor.OpenAsync(path);

        editor.TabLabels.ShouldBe(new[] { "hero.achx" }, Case.Sensitive, "No means the file is not opened, so it gets no tab");
        editor.ErrorBannerText.ShouldBeNull("declining is not an error");
        File.ReadAllBytes(path).ShouldBe(before);
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run" }, "the document that was open stays open");
        editor.Click(editor.RowButton(editor.ChainNamed("Run"), "Add Frame"));
        AnimationEditorHarness.ReadSaved(hero).AnimationChains.Single().Frames.Count.ShouldBe(2, "and its edits still go to its own file");
    }

    [AvaloniaFact]
    public async Task LegacyUvFile_WithAMissingTexture_IsRefused_AndTheMessageNamesTheTexture()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteUvAchx(editor, "old.achx", ("Walk", 0f, 0f, 0.25f, 0.25f));

        await editor.OpenAsync(path);

        editor.TabLabels.ShouldBeEmpty("a UV file cannot be converted without its texture, so it does not open");
        editor.ErrorBannerText.ShouldNotBeNull("the user is told why");
        editor.ErrorBannerText!.ShouldContain("sheet.png");
        editor.Dialogs.Shown.ShouldBeEmpty("no conversion question is asked when it cannot be done");
    }

    [AvaloniaFact]
    public async Task NonAsciiFolderAndFileNames_OpenEditAndSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string folder = Path.Combine(editor.ProjectFolder, "héros 日本 🎮");
        Directory.CreateDirectory(folder);
        editor.WritePng(Path.Combine(folder, "šprite ünits.png"), 64, 64);
        string path = editor.WriteAchx(Path.Combine(folder, "héros.achx"), AnimationEditorHarness.Chain("Marche", "šprite ünits.png", (0, 0, 16, 16)));

        await editor.OpenAsync(path);

        editor.ThrowIfErrorShown();
        editor.TabLabels.ShouldBe(new[] { "héros.achx" });
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));
        AnimationChainSave marche = editor.ChainNamed("Marche");
        editor.Expand(marche);
        editor.ClickRow(marche.Frames[0]);
        editor.TypeNumber("PropPixelX", "8");
        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On");
        AnimationFrameSave saved = AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single();
        saved.LeftCoordinate.ShouldBe(8);
        saved.TextureName.ShouldBe("šprite ünits.png", "the texture path keeps its characters");
    }

    [AvaloniaFact]
    public async Task NonPowerOfTwoTexture_OddPixelRectangles_RoundTripExactly()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("strip.png", 1000, 7);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "strip.png", (333, 1, 334, 5), (993, 0, 7, 7)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");

        editor.PixelRectOf(walk.Frames[0]).ShouldBe((333, 1, 334, 5));
        editor.PixelRectOf(walk.Frames[1]).ShouldBe((993, 0, 7, 7));
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[1]);
        editor.TypeNumber("PropPixelY", "0");

        AnimationChainListSave saved = AnimationEditorHarness.ReadSaved(path);
        AnimationFrameSave first = saved.AnimationChains.Single().Frames[0];
        AnimationFrameSave second = saved.AnimationChains.Single().Frames[1];
        (first.LeftCoordinate, first.TopCoordinate, first.RightCoordinate, first.BottomCoordinate).ShouldBe((333f, 1f, 667f, 6f), "UV in memory must not drift the pixels on disk");
        (second.LeftCoordinate, second.TopCoordinate, second.RightCoordinate, second.BottomCoordinate).ShouldBe((993f, 0f, 1000f, 7f));
    }

    [AvaloniaFact]
    public async Task OnePixelTexture_FitsToView_AndAnAddedFrameCoversThatPixel()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("dot.png", 1, 1);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Blink", "dot.png", (0, 0, 1, 1)));
        await editor.OpenAsync(path);
        AnimationChainSave blink = editor.ChainNamed("Blink");

        editor.DoubleClickRow(blink);
        editor.Wheel(editor.WireframePointAt(0.5f, 0.5f), 1, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        float zoom = editor.Wireframe.CameraState.Item3;
        float.IsFinite(zoom).ShouldBeTrue();
        zoom.ShouldBeGreaterThan(0);
        editor.Click(editor.RowButton(blink, "Add Frame"));
        editor.ThrowIfErrorShown();
        editor.PixelRectOf(blink.Frames[1]).ShouldBe((0, 0, 1, 1), "a frame added on a 1 px texture covers the pixel");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task ReadOnlyAchx_TakesTheEdit_ReportsTheFailedAutoSave_AndSavesOnceWritable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            editor.TypeNumber("PropPixelX", "8");

            editor.PixelRectOf(walk.Frames[0]).X.ShouldBe(8, "the edit itself is kept");
            editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save Failed");
            editor.ToastText.ShouldNotBeNull("the user is told the save did not happen");
            AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().LeftCoordinate.ShouldBe(0);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        editor.TypeNumber("PropPixelX", "16");

        editor.Control<TextBlock>("StatusSaveLabel").Text.ShouldBe("Auto Save On", "the next edit saves again once the file is writable");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().LeftCoordinate.ShouldBe(16);
    }

    [AvaloniaFact]
    public async Task TextureAboveTheAchxFolder_ViaDotDot_Loads_AndTheSavedPathStaysRelative()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "data", "anims"));
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx(Path.Combine("data", "anims", "hero.achx"), AnimationEditorHarness.Chain("Walk", "../../sheet.png", (0, 0, 16, 16)));

        await editor.OpenAsync(path);

        editor.ThrowIfErrorShown();
        editor.Wireframe.BitmapSize.ShouldBe((64, 64), "a texture two folders up resolves against the file's folder");
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.Control<TextBox>("PropTextureName").Text.ShouldBe("../../sheet.png", "the inspector shows the path as stored");
        editor.TypeNumber("PropPixelX", "8");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().TextureName.ShouldBe("../../sheet.png");
    }

    [AvaloniaFact]
    public async Task TextureInASubfolder_Loads_AndTheSavedPathStaysRelative()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "art", "sprites"));
        editor.WritePng(Path.Combine("art", "sprites", "sheet.png"), 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "art/sprites/sheet.png", (0, 0, 16, 16)));

        await editor.OpenAsync(path);

        editor.ThrowIfErrorShown();
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        editor.TypeNumber("PropPixelX", "8");
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Single().Frames.Single().TextureName.ShouldBe("art/sprites/sheet.png");
    }

    [AvaloniaFact]
    public async Task TwoHundredChains_Open_Filter_Sort_ArrowDown_AndDelete_AllBehave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        // Written in reverse so the sort has work to do.
        AnimationChainSave[] chains = Enumerable.Range(0, 200).Reverse()
            .Select(i => AnimationEditorHarness.Chain($"Chain{i:000}", "sheet.png", (0, 0, 16, 16))).ToArray();
        string path = editor.WriteAchx("many.achx", chains);
        await editor.OpenAsync(path);

        editor.Nodes.Count(node => node.IsChainNode).ShouldBe(200);
        editor.Click(editor.Control<Button>("SearchToggleBtn"));
        editor.TypeAndEnter(editor.Control<TextBox>("SearchBox"), "Chain19");
        editor.VisibleChainHeaders.Count.ShouldBe(10, "Chain190..Chain199 match");
        editor.Click(editor.Control<Button>("SearchClearBtn"));
        editor.VisibleChainHeaders.Count.ShouldBe(200);

        editor.RightClickRow(editor.ChainNamed("Chain000"));
        editor.PickTreeMenuItem("Sort Animations Alphabetically");
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(Enumerable.Range(0, 200).Select(i => $"Chain{i:000}"));

        editor.ClickRow(editor.ChainNamed("Chain000"));
        editor.Press(Key.Down);
        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe("Chain001");
        editor.Press(Key.Delete);
        editor.Project.AnimationChains.Count.ShouldBe(199);
        editor.Project.AnimationChains.Any(chain => chain.Name == "Chain001").ShouldBeFalse();
        AnimationEditorHarness.ReadSaved(path).AnimationChains.Count.ShouldBe(199);
    }

    /// <summary>A legacy UV-coordinate file, which the editor converts on open after asking.</summary>
    private static string WriteUvAchx(AnimationEditorHarness editor, string name, params (string Name, float Left, float Top, float Right, float Bottom)[] chains)
    {
        string path = Path.Combine(editor.ProjectFolder, name);
        AnimationChainListSave list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        foreach ((string chainName, float left, float top, float right, float bottom) in chains)
        {
            AnimationChainSave chain = new AnimationChainSave { Name = chainName };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = "sheet.png",
                FrameLength = 0.1f,
                LeftCoordinate = left,
                TopCoordinate = top,
                RightCoordinate = right,
                BottomCoordinate = bottom,
            });
            list.AnimationChains.Add(chain);
        }
        list.Save(path);
        return path;
    }
}
