using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// What the composited sequence preview draws for each frame's collision shapes (rects, circles, polygons): the
/// active frame's shapes in entity space, for visible tracks inside their chain,
/// stacked like the sprites, and hidden by the Shapes toggle. Shapes are read from the same snapshot the draw uses
/// (<see cref="PreviewControl.GetShapeInfosForTest"/>), and time moves by ruler clicks or the fake tick clock.
/// </summary>
public class SequencePreviewShapesScenarioTests
{
    private sealed class FakeTime
    {
        public long Now;
        public TickClock Clock => new(() => Now, 1000);
    }

    private sealed record DrawnShape(PreviewControl.PreviewShapeKind Kind, float X, float Y, float P1, float P2, float[]? Points = null);

    // hero.achx on hero.png.
    //   Guard, 0.1 s frames: 0 rect (5,2) 4x3; 1 circle (-3,1) r6; 2 no shapes; 3 polygon origin (1,1), three vertices.
    //   Slide, two 0.1 s frames, both offset (10,7): a rect (1,1) 2x2.
    // fx.achx on fx.png: Spark, 0.1 s frames: 0 rect (-20,0) 2x2; 1 circle (-20,0) r3.
    private static void WriteSources(AnimationEditorHarness editor)
    {
        editor.WritePng("hero.png", 64, 64);
        editor.WritePng("fx.png", 32, 32);

        var guard = AnimationEditorHarness.Chain("Guard", "hero.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16));
        guard.Frames[0].ShapesSave = Shapes(new AARectSave { Name = "Hit", X = 5, Y = 2, ScaleX = 4, ScaleY = 3 });
        guard.Frames[1].ShapesSave = Shapes(new CircleSave { Name = "Ring", X = -3, Y = 1, Radius = 6 });
        guard.Frames[3].ShapesSave = Shapes(new PolygonSave
        {
            Name = "Wedge", X = 1, Y = 1,
            Points = { new Vector2Save { X = 2, Y = 3 }, new Vector2Save { X = 5, Y = 3 }, new Vector2Save { X = 5, Y = 6 }, new Vector2Save { X = 2, Y = 3 } },
        });

        var slide = AnimationEditorHarness.Chain("Slide", "hero.png", (0, 0, 16, 16), (16, 0, 16, 16));
        foreach (var frame in slide.Frames)
        {
            frame.RelativeX = 10;
            frame.RelativeY = 7;
        }
        slide.Frames[0].ShapesSave = Shapes(new AARectSave { Name = "Box", X = 1, Y = 1, ScaleX = 2, ScaleY = 2 });

        editor.WriteAchx("hero.achx", guard, slide);

        var spark = AnimationEditorHarness.Chain("Spark", "fx.png", (0, 0, 16, 16), (16, 0, 16, 16));
        spark.Frames[0].ShapesSave = Shapes(new AARectSave { Name = "Flash", X = -20, Y = 0, ScaleX = 2, ScaleY = 2 });
        spark.Frames[1].ShapesSave = Shapes(new CircleSave { Name = "Puff", X = -20, Y = 0, Radius = 3 });
        editor.WriteAchx("fx.achx", spark);
    }

    private static ShapesSave Shapes(object shape)
    {
        var shapes = new ShapesSave();
        shapes.Add(shape);
        return shapes;
    }

    private static SequenceTrackSave Track(string name, bool visible, params (string Chain, double Start)[] chains)
    {
        var track = new SequenceTrackSave { Name = name, Visible = visible };
        foreach (var (chain, start) in chains)
            track.Chains.Add(new SequenceChainReferenceSave
            {
                Source = chain == "Spark" ? "fx.achx" : "hero.achx", Chain = chain, Start = start, Loops = 1,
            });
        return track;
    }

    private static SequenceTrackSave Plain(string chain, double start = 0) => Track(chain, true, (chain, start));

    private static SequenceSave Sequence(string name, params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave { Name = name };
        sequence.Tracks.AddRange(tracks);
        return sequence;
    }

    private static async Task OpenAsync(AnimationEditorHarness editor, params SequenceSave[] sequences)
    {
        WriteSources(editor);
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx", "fx.achx" } };
        save.Sequences.AddRange(sequences);
        save.Save(path);
        await editor.OpenAsync(path);
        editor.Layout();
    }

    private static PreviewControl Preview(AnimationEditorHarness editor) => editor.Control<PreviewControl>("SequencePreview");

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static void Scrub(AnimationEditorHarness editor, double time) =>
        editor.ClickAt(editor.PointIn(Timeline(editor), Timeline(editor).Viewport.XAt(time), TimelineViewport.RulerHeight / 2));

    private static List<DrawnShape> Current(AnimationEditorHarness editor) =>
        Preview(editor).GetShapeInfosForTest().Select(s => new DrawnShape(s.Kind, s.X, s.Y, s.Param1, s.Param2, s.Points)).ToList();

    private static DrawnShape Rect(float x, float y, float w, float h) => new(PreviewControl.PreviewShapeKind.Rect, x, y, w, h);

    private static DrawnShape Circle(float x, float y, float r) => new(PreviewControl.PreviewShapeKind.Circle, x, y, r, 0);

    private static void ShouldBeShapes(List<DrawnShape> actual, params DrawnShape[] expected)
    {
        actual.Select(d => (d.Kind, d.X, d.Y, d.P1, d.P2)).ShouldBe(expected.Select(d => (d.Kind, d.X, d.Y, d.P1, d.P2)));
    }

    [AvaloniaFact]
    public async Task OnlyTheActiveFramesShapesDraw_AtTheirFrameOffsets_AndTheyChangeAsThePlayheadMoves()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Sequence("Attack", Plain("Guard")));

        Scrub(editor, 0.05);
        ShouldBeShapes(Current(editor), Rect(5, 2, 4, 3));
        Scrub(editor, 0.15);
        ShouldBeShapes(Current(editor), Circle(-3, 1, 6));
        // Frame 2 holds no shapes: nothing of frames 0, 1 or 3 lingers.
        Scrub(editor, 0.25);
        Current(editor).ShouldBeEmpty();
        Scrub(editor, 0.35);
        var wedge = Current(editor).Single();
        (wedge.Kind, wedge.X, wedge.Y).ShouldBe((PreviewControl.PreviewShapeKind.Polygon, 1f, 1f));
        wedge.Points.ShouldBe(new[] { 2f, 3f, 5f, 3f, 5f, 6f });
    }

    [AvaloniaFact]
    public async Task WhilePlaying_TheShapesFollowTheFakeClock()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Sequence("Attack", Plain("Guard")));
        FakeTime time = new FakeTime();
        editor.Window.SetSequencePlaybackClock(time.Clock);

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        ShouldBeShapes(Current(editor), Rect(5, 2, 4, 3));
        time.Now += 150;
        editor.Window.TickSequencePlayback();
        ShouldBeShapes(Current(editor), Circle(-3, 1, 6));
        time.Now += 100;
        editor.Window.TickSequencePlayback();
        Current(editor).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task TheFramesOwnOffsetMovesTheSprite_NotTheShape()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Sequence("Attack", Plain("Slide")));

        Scrub(editor, 0.05);

        var sprite = Preview(editor).Composite!.Single().Sample();
        (sprite.OffsetX, sprite.OffsetY).ShouldBe((10f, 7f));
        // The shape is entity space: the frame offset does not move it.
        ShouldBeShapes(Current(editor), Rect(1, 1, 2, 2));
    }

    [AvaloniaFact]
    public async Task AHiddenTrack_DrawsNoShapes_AndTheOthersStillDo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Sequence("Attack",
            Track("Fx", true, ("Spark", 0)),
            Track("Body", false, ("Guard", 0))));

        Scrub(editor, 0.05);

        ShouldBeShapes(Current(editor), Rect(-20, 0, 2, 2));
    }

    [AvaloniaFact]
    public async Task OutsideItsChain_ATrackDrawsNoShapes_BeforeItStartsAndAfterItEnds()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Guard plays 0.2 s to 0.6 s.
        await OpenAsync(editor, Sequence("Attack", Plain("Guard", 0.2), Plain("Spark", 0.8)));

        Scrub(editor, 0.1);
        Current(editor).ShouldBeEmpty();
        Scrub(editor, 0.25);
        ShouldBeShapes(Current(editor), Rect(5, 2, 4, 3));
        // Between the two chains nothing is active.
        Scrub(editor, 0.7);
        Current(editor).ShouldBeEmpty();
        Scrub(editor, 0.85);
        ShouldBeShapes(Current(editor), Rect(-20, 0, 2, 2));
    }

    [AvaloniaFact]
    public async Task ACutChain_StopsDrawingItsShapes_WhenTheNextChainOnTheTrackStarts()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Guard would run 0 to 0.4 s; Spark starts at 0.15 s on the same track and cuts it.
        var track = Track("Body", true, ("Guard", 0), ("Spark", 0.15));
        await OpenAsync(editor, Sequence("Attack", track));

        Scrub(editor, 0.1);
        ShouldBeShapes(Current(editor), Circle(-3, 1, 6));
        // Guard's frame 1 would still be on screen at 0.18 s; Spark's first frame draws instead.
        Scrub(editor, 0.18);
        ShouldBeShapes(Current(editor), Rect(-20, 0, 2, 2));
        // Past Spark's end nothing draws; Guard does not come back.
        Scrub(editor, 0.36);
        Current(editor).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Tracks_StackLikeTheSprites_TheTopLaneLastSoItIsInFront()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Sequence("Attack", Plain("Spark"), Plain("Guard")));

        Scrub(editor, 0.05);

        // Bottom lane (Guard) first, top lane (Spark) last, the same order as the sprites.
        ShouldBeShapes(Current(editor), Rect(5, 2, 4, 3), Rect(-20, 0, 2, 2));
        Preview(editor).Composite!.Select(sprite => sprite.Chain.Name).ShouldBe(new[] { "Guard", "Spark" });
    }

    [AvaloniaFact]
    public async Task SeveralSelectedSequences_EachDrawTheirOwnShapes_InSelectionOrder()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor,
            Sequence("Attack", Plain("Guard")),
            Sequence("Aura", Plain("Spark")));
        ListBox list = editor.Control<ListBox>("SequenceList");

        editor.Click((Avalonia.Controls.Control)list.ContainerFromIndex(1)!);
        ShouldBeShapes(Current(editor), Rect(-20, 0, 2, 2));
        editor.Click((Avalonia.Controls.Control)list.ContainerFromIndex(0)!, RawInputModifiers.Control);

        // Aura was selected first, so it is at the back.
        ShouldBeShapes(Current(editor), Rect(-20, 0, 2, 2), Rect(5, 2, 4, 3));
    }

    [AvaloniaFact]
    public async Task TheShapesToggle_HidesAndShowsThem_AndTheAchxPreviewsToggleFollows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Sequence("Attack", Plain("Guard")));
        ToggleButton toggle = editor.Control<ToggleButton>("SequenceShowShapes");

        toggle.IsChecked.ShouldBe(true);
        Current(editor).ShouldNotBeEmpty();
        toggle.IsChecked = false;
        Current(editor).ShouldBeEmpty();
        // The sprite is unaffected.
        Preview(editor).Composite!.Count.ShouldBe(1);
        editor.Control<ToggleButton>("ShowShapesCheck").IsChecked.ShouldBe(false);

        toggle.IsChecked = true;
        ShouldBeShapes(Current(editor), Rect(5, 2, 4, 3));
    }

    [AvaloniaFact]
    public async Task ZoomAndPan_DoNotChangeWhichShapesDraw()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor, Sequence("Attack", Track("Body", true, ("Guard", 0))));
        Scrub(editor, 0.15);
        var before = Current(editor);

        Preview(editor).SetZoomPercent(300);
        Preview(editor).SetPan(37, -12);

        Current(editor).ShouldBe(before);
        ShouldBeShapes(Current(editor), Circle(-3, 1, 6));
    }
}
