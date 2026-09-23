using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The other document kinds: a Tiled .tsx opened natively (grid locked on, no shapes, flips,
/// offsets or colour, one footprint per chain, durations written back to the tileset) and the
/// .achj JSON format through the ordinary flows; plus the timeline strip's playhead.
/// </summary>
public class TsxAndAchjScenarioTests
{
    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static async Task<AnimationChainSave> OpenTsxAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("Heroes.png", 64, 64);
        string tsx = Path.Combine(editor.ProjectFolder, "Heroes.tsx");
        File.WriteAllText(tsx, TsxXml);
        await editor.OpenAsync(tsx);
        editor.Services.ProjectManager.IsNativeTsxProject.ShouldBeTrue();
        return editor.Project.AnimationChains.Single();
    }

    [AvaloniaFact]
    public async Task Achj_OpensAndAutoSavesAsJson_WithShapesAndColour()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = Path.Combine(editor.ProjectFolder, "hero.achj");
        AnimationChainListSave fixture = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        fixture.AnimationChains.Add(AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        fixture.SaveJson(path);
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.RightClickRow(walk.Frames[0]);
        editor.PickTreeMenuItem("Add AxisAlignedRectangle");
        editor.ClickRow(walk.Frames[0]);
        editor.TypeNumber("PropRed", "77");

        editor.Press(Key.S, RawInputModifiers.Control);

        File.ReadAllText(path).TrimStart().ShouldStartWith("{", customMessage: "an .achj stays JSON");
        AnimationChainListSave saved = AnimationChainListSave.FromFile(path);
        saved.AnimationChains.Single().Frames.Single().ShapesSave!.AARectSaves.Count().ShouldBe(1);
        saved.AnimationChains.Single().Frames.Single().Red.ShouldBe(77);
        saved.AnimationChains.Single().Frames.Single().LeftCoordinate.ShouldBe(0f);
        saved.AnimationChains.Single().Frames.Single().RightCoordinate.ShouldBe(16f);
    }

    [AvaloniaFact]
    public async Task SaveAs_FromAchxToAchj_WritesJson_AndLaterEditsGoThere()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string achx = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(achx);
        string achj = Path.Combine(editor.ProjectFolder, "hero.achj");

        editor.Dialogs.AnswerNextSaveFile(achj);
        editor.ClickMenu("MenuSaveAs");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));

        File.ReadAllText(achj).TrimStart().ShouldStartWith("{");
        AnimationChainListSave.FromFile(achj).AnimationChains.Single().Frames.Count.ShouldBe(2);
        AnimationChainListSave.FromFile(achx).AnimationChains.Single().Frames.Count.ShouldBe(1, "the old file is left alone");
        editor.Tabs.ActiveTab!.Path.FullPath.ShouldEndWith("hero.achj");
    }

    [AvaloniaFact]
    public async Task TimelineStrip_ScrubbingMovesThePlayheadFrame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        if (editor.Preview.IsPlaying)
        {
            editor.Press(Key.Space);
        }
        Border surface = editor.Control<Border>("TimelineScrubSurface");
        ItemsControl strip = editor.Control<ItemsControl>("TimelineStrip");
        List<AnimationEditor.Core.ViewModels.TimelineFrameVm> frames = strip.ItemsSource!.Cast<AnimationEditor.Core.ViewModels.TimelineFrameVm>().ToList();
        frames.Count.ShouldBe(3);

        editor.ClickAt(editor.PointIn(surface, surface.Bounds.Width * 0.95, surface.Bounds.Height / 2));
        frames.Select(frame => frame.IsCurrent).ShouldBe(new[] { false, false, true });

        editor.ClickAt(editor.PointIn(surface, surface.Bounds.Width * 0.02, surface.Bounds.Height / 2));
        frames.Select(frame => frame.IsCurrent).ShouldBe(new[] { true, false, false });
        editor.Preview.Playback.CurrentFrameIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task Tsx_ContextMenus_OfferNoFlipsShapesOrOffsets()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);

        editor.RightClickRow(chain);
        List<string> chainMenu = editor.TreeMenuHeaders;
        editor.PickTreeMenuItem("Add Frame");
        chainMenu.ShouldNotContain("Flip Horizontally");
        chainMenu.ShouldNotContain("Adjust Offsets…");
        chainMenu.ShouldContain("Duplicate");
        chain.Frames.Count.ShouldBe(3);

        editor.Expand(chain);
        editor.RightClickRow(chain.Frames[0]);
        List<string> frameMenu = editor.TreeMenuHeaders;
        editor.PickTreeMenuItem("Delete Frame");
        frameMenu.ShouldNotContain("Add AxisAlignedRectangle");
        frameMenu.ShouldNotContain("Add Circle");
        chain.Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task Tsx_DeletingTheChain_ThenUndo_RoundTripsThroughTheTileset()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        string tsx = Path.Combine(editor.ProjectFolder, "Heroes.tsx");
        editor.ClickRow(chain);

        editor.Press(Key.Delete);
        editor.Project.AnimationChains.ShouldBeEmpty();
        File.ReadAllText(tsx).ShouldNotContain("<animation>", customMessage: "the tileset no longer carries the animation");

        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Project.AnimationChains.Count.ShouldBe(1);
        File.ReadAllText(tsx).ShouldContain("<animation>");
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task Tsx_FrameLengthEdit_WritesTheDurationBackInMilliseconds()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        chain.Frames[0].FrameLength.ShouldBe(0.2f);
        editor.Expand(chain);
        editor.ClickRow(chain.Frames[0]);

        editor.TypeFlanker("PropFrameLen", "0.25");

        chain.Frames[0].FrameLength.ShouldBe(0.25f);
        File.ReadAllText(Path.Combine(editor.ProjectFolder, "Heroes.tsx")).ShouldContain("duration=\"250\"");
    }

    [AvaloniaFact]
    public async Task Tsx_GridCannotBeTurnedOff_AndInspectorHidesAchxOnlySections()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        editor.ClickRow(chain);
        ToggleButton grid = editor.Control<ToggleButton>("SnapToGridCheck");
        grid.IsChecked.ShouldBe(true);

        editor.Click(grid);

        grid.IsChecked.ShouldBe(true, "a tileset's grid is its tile size and cannot be turned off");
        editor.Control<Control>("PropChainTsxOwnerSection").IsVisible.ShouldBeTrue();
        editor.Control<CheckBox>("PropChainLoop").IsEnabled.ShouldBeFalse();
        editor.Expand(chain);
        editor.ClickRow(chain.Frames[0]);
        editor.Control<Control>("PropTransformSection").IsVisible.ShouldBeFalse();
        editor.Control<Control>("PropColorSection").IsVisible.ShouldBeFalse();
        editor.PixelRectOf(chain.Frames[0]).ShouldBe((0, 0, 16, 16));
        editor.PixelRectOf(chain.Frames[1]).ShouldBe((16, 0, 16, 16));
    }

    [AvaloniaFact]
    public async Task Tsx_ResizingOneFrame_GivesEveryFrameOfTheChainTheSameFootprint()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        editor.Expand(chain);
        editor.ClickRow(chain.Frames[0]);

        editor.TypeNumber("PropPixelW", "32");

        editor.PixelRectOf(chain.Frames[0]).Width.ShouldBe(32);
        editor.PixelRectOf(chain.Frames[1]).Width.ShouldBe(32, "a tileset animation has one footprint for all its frames");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.PixelRectOf(chain.Frames[1]).Width.ShouldBe(16, "and one undo restores all of them");
    }

    [AvaloniaFact]
    public async Task Tsx_TypingAFreeOwnerTile_MovesTheAnimationToThatTile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        editor.ClickRow(chain);
        editor.Services.ProjectManager.GetTsxOwnerTileId(chain).ShouldBe(0u);

        editor.TypeNumber("PropChainTsxOwnerInput", "5");

        editor.Control<Control>("PropChainTsxOwnerError").IsVisible.ShouldBeFalse();
        editor.Services.ProjectManager.GetTsxOwnerTileId(chain).ShouldBe(5u);
        File.ReadAllText(Path.Combine(editor.ProjectFolder, "Heroes.tsx")).ShouldContain("<tile id=\"5\">");
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Services.ProjectManager.GetTsxOwnerTileId(chain).ShouldBe(0u);
    }
}
