using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>An Inspector field changes only what was typed in it: not the other fields, not values it can not show exactly, and nothing out of range.</summary>
public class InspectorFieldFidelityScenarioTests
{
    private static async Task<AnimationEditorHarness> Open(Action<AnimationChainSave> setup)
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave source = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        setup(source);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", source));
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        foreach (var frame in walk.Frames)
            if (frame.ShapesSave is { Shapes.Count: > 0 }) editor.Expand(frame);
        return editor;
    }

    private static void AddRect(AnimationFrameSave frame, string name, float x, float y, float scaleX, float scaleY)
    {
        frame.ShapesSave ??= new ShapesSave();
        frame.ShapesSave.Add(new AARectSave { Name = name, X = x, Y = y, ScaleX = scaleX, ScaleY = scaleY });
    }

    private static void TypeVertex(AnimationEditorHarness editor, string name, string text)
    {
        var field = editor.Control<StackPanel>("PropPolygonVertices").GetVisualDescendants().OfType<NumericUpDown>().Single(n => n.Name == name);
        editor.TypeAndEnter(field.GetVisualDescendants().OfType<TextBox>().First(), text);
    }

    [AvaloniaFact]
    public async Task RectName_FocusInAndOut_ChangesNothing_WhenAnOffsetHasManyDigits()
    {
        using var editor = await Open(chain => AddRect(chain.Frames[0], "A", 1f / 3f, 10f / 7f, 4.1f / 3f, 4));
        var rect = editor.ChainNamed("Walk").Frames[0].ShapesSave!.AARectSaves.First();
        editor.ClickRow(rect);
        float x = rect.X, scaleX = rect.ScaleX;

        editor.Control<TextBox>("PropRectName").Focus();
        editor.Press(Key.Tab);
        editor.ClickRow(editor.ChainNamed("Walk").Frames[1]);

        editor.UndoLabels.ShouldBeEmpty();
        rect.X.ShouldBe(x);
        rect.ScaleX.ShouldBe(scaleX);
    }

    [AvaloniaFact]
    public async Task PixelX_LeavesAFractionalTopAndSizeAsTheyWere()
    {
        using var editor = await Open(chain =>
        {
            chain.Frames[0].TopCoordinate = 0.5f;
            chain.Frames[0].BottomCoordinate = 16.25f;
            chain.Frames[0].RightCoordinate = 16.5f;
        });
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        editor.TypeNumber("PropPixelX", "8");

        frame.LeftCoordinate.ShouldBe(8f);
        (frame.TopCoordinate, frame.BottomCoordinate).ShouldBe((0.5f, 16.25f));
    }

    [AvaloniaFact]
    public async Task PolygonAndCircleNames_FocusInAndOut_ChangeNothing_WhenAnOffsetHasManyDigits()
    {
        using var editor = await Open(chain =>
        {
            chain.Frames[0].ShapesSave = new ShapesSave();
            chain.Frames[0].ShapesSave!.Add(new CircleSave { Name = "C", X = 1f / 3f, Radius = 10f / 7f });
            chain.Frames[1].ShapesSave = new ShapesSave();
            chain.Frames[1].ShapesSave!.Add(new CircleSave { Name = "D", X = 2f / 3f, Radius = 5 });
        });
        var walk = editor.ChainNamed("Walk");
        var circle = walk.Frames[0].ShapesSave!.CircleSaves.First();
        editor.ClickRow(circle);

        editor.Control<TextBox>("PropCircleName").Focus();
        editor.ClickRow(walk.Frames[1]);

        editor.UndoLabels.ShouldBeEmpty();
        circle.X.ShouldBe(1f / 3f);
    }

    [AvaloniaFact]
    public async Task FrameOffsetX_LeavesAManyDigitOffsetYExactlyAsItWas()
    {
        using var editor = await Open(chain => { chain.Frames[0].RelativeX = 2; chain.Frames[0].RelativeY = 1f / 3f; });
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        editor.TypeNumber("PropRelX", "5");

        frame.RelativeY.ShouldBe(1f / 3f);
    }

    [AvaloniaFact]
    public async Task PixelX_KeepsAFractionalWidth()
    {
        using var editor = await Open(chain => chain.Frames[0].RightCoordinate = 16.5f);
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        editor.TypeNumber("PropPixelX", "8");

        (frame.LeftCoordinate, frame.RightCoordinate).ShouldBe((8f, 24.5f));
    }

    [AvaloniaFact]
    public async Task PolygonX_AndVertexX_LeaveAManyDigitYExactlyAsItWas()
    {
        using var editor = await Open(chain =>
        {
            chain.Frames[0].ShapesSave = new ShapesSave();
            chain.Frames[0].ShapesSave!.Add(new PolygonSave
            {
                Name = "P", X = 1, Y = 1f / 3f,
                Points = { new Vector2Save { X = 0, Y = 10f / 7f }, new Vector2Save { X = 8, Y = 0 }, new Vector2Save { X = 0, Y = 8 } },
            });
        });
        var polygon = editor.ChainNamed("Walk").Frames[0].ShapesSave!.PolygonSaves.First();
        editor.ClickRow(polygon);

        editor.TypeNumber("PropPolygonX", "5");
        (polygon.X, polygon.Y).ShouldBe((5f, 1f / 3f));

        TypeVertex(editor, "PropPolygonVertex0X", "3");
        (polygon.Points[0].X, polygon.Points[0].Y).ShouldBe((3f, 10f / 7f));
    }

    [AvaloniaFact]
    public async Task FrameLength_FocusInAndOut_ChangesNothing_WhenItHasMoreDigitsThanTheFieldShows()
    {
        using var editor = await Open(chain => chain.Frames[0].FrameLength = 1f / 30f);
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        editor.Click(editor.Control<FlankerNumericField>("PropFrameLen").ValueBox);
        editor.ClickRow(editor.ChainNamed("Walk").Frames[1]);

        editor.UndoLabels.ShouldBeEmpty();
        frame.FrameLength.ShouldBe(1f / 30f);
    }

    [AvaloniaFact]
    public async Task Red_FocusInAndOut_ChangesNothing_WhenTheFileHoldsAValueOutOfRange()
    {
        using var editor = await Open(chain => chain.Frames[0].Red = 300);
        var frame = editor.ChainNamed("Walk").Frames[0];
        editor.ClickRow(frame);

        editor.Control<NumericUpDown>("PropRed").Focus();
        editor.Control<NumericUpDown>("PropGreen").Focus();

        editor.UndoLabels.ShouldBeEmpty();
        frame.Red.ShouldBe(300);
    }

    [AvaloniaFact]
    public async Task NameBoxes_FocusInAndOut_ChangeNothing_WhenANameHasSpacesAround()
    {
        using var editor = await Open(chain => AddRect(chain.Frames[0], "A", 0, 0, 4, 4));
        var walk = editor.ChainNamed("Walk");
        walk.Name = " Walk ";
        editor.ClickRow(walk.Frames[0]);
        editor.ClickRow(walk);

        editor.Control<TextBox>("PropChainName").Focus();
        editor.Control<CheckBox>("PropChainLoop").Focus();

        editor.UndoLabels.ShouldBeEmpty();
        walk.Name.ShouldBe(" Walk ");
    }

    [AvaloniaFact]
    public async Task MixedTexture_FocusInAndOut_KeepsEachFramesTexture()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WritePng("other.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)),
            AnimationEditorHarness.Chain("Run", "other.png", (32, 0, 20, 24))));
        var walk = editor.ChainNamed("Walk").Frames[0];
        var run = editor.ChainNamed("Run").Frames[0];
        editor.Expand(editor.ChainNamed("Walk"));
        editor.Expand(editor.ChainNamed("Run"));
        editor.ClickRow(walk);
        editor.ClickRow(run, RawInputModifiers.Control);

        editor.Control<TextBox>("PropTextureName").Focus();
        editor.Control<NumericUpDown>("PropPixelX").Focus();

        editor.UndoLabels.ShouldBeEmpty();
        (walk.TextureName, run.TextureName).ShouldBe(("sheet.png", "other.png"));
    }

    [AvaloniaFact]
    public async Task SequenceFields_FocusInAndOut_ChangeNothing_WhenTheyHoldMoreThanTheyShow()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        walk.Frames[0].FrameLength = 1f / 3f;
        editor.WriteAchx("hero.achx", walk);
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var sequence = new SequenceSave { Name = "Attack" };
        var track = new SequenceTrackSave { Name = " Body " };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 1.0 / 3 });
        sequence.Tracks.Add(track);
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.Add(sequence);
        save.Save(path);
        string before = File.ReadAllText(path);
        await editor.OpenAsync(path);
        var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
        var inspector = editor.Control<SequenceInspectorControl>("SequenceInspector");

        editor.ClickAt(editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(0) + 10));
        inspector.TrackNameBox.Focus();
        inspector.TrackVisibleBox.Focus();
        editor.ClickAt(editor.PointIn(timeline, timeline.Viewport.XAt(0.5),
            timeline.Viewport.LaneTop(0) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2));
        inspector.ChainPanel.IsEffectivelyVisible.ShouldBeTrue();
        inspector.ChainStartField.ValueBox.Focus();
        inspector.ChainLengthField.ValueBox.Focus();
        inspector.ChainStartField.ValueBox.Focus();

        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllText(path).ShouldBe(before);
    }
}
