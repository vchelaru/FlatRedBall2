using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Text that XML cannot store is refused by the rename box and every Inspector text box, naming the character (#2046).</summary>
public class UnstorableTextScenarioTests
{
    private static readonly string Bad = "Bad" + (char)1;

    private static async Task<(AnimationEditorHarness Editor, string Path, AnimationChainSave Walk)> OpenAsync(
        string extension = "achx", string rectName = "Hit")
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16));
        walk.Frames[0].Events.Add(new AnimationFrameEvent { Name = "Step", Data = "left" });
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = rectName, ScaleX = 4, ScaleY = 4 });
        walk.Frames[0].ShapesSave!.Add(new CircleSave { Name = "Ball", Radius = 4 });
        walk.Frames[0].ShapesSave!.Add(new PolygonSave
        {
            Name = "Hull",
            Points = [new Vector2Save(), new Vector2Save { X = 4 }, new Vector2Save { X = 4, Y = 4 }],
        });
        AnimationChainListSave list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel, AnimationChains = [walk] };
        string path = Path.Combine(editor.ProjectFolder, "hero." + extension);
        if (extension == "achj") list.SaveJson(path);
        else list.Save(path);
        await editor.OpenAsync(path);
        AnimationChainSave loaded = editor.ChainNamed("Walk");
        editor.Expand(loaded);
        editor.Expand(loaded.Frames[0]);
        return (editor, path, loaded);
    }

    private static void ShouldBeRefused(AnimationEditorHarness editor, string path, TextBox box, string kept)
    {
        byte[] before = File.ReadAllBytes(path);
        box.Focus();
        editor.Layout();
        // What a paste leaves in the box; a control character has no key.
        box.Text = Bad;
        editor.Press(Key.Enter);

        editor.ProblemText.ShouldBe($"\"Bad{(char)0xFFFD}\" contains the character U+0001, which XML cannot store.");
        box.Text.ShouldBe(kept);
        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllBytes(path).ShouldBe(before);
    }

    [AvaloniaTheory]
    [InlineData("achx")]
    [InlineData("achj")]
    public async Task InspectorChainName_IsRefused(string extension)
    {
        var (editor, path, walk) = await OpenAsync(extension);
        using var _ = editor;
        editor.ClickRow(walk);

        ShouldBeRefused(editor, path, editor.Control<TextBox>("PropChainName"), "Walk");
    }

    [AvaloniaTheory]
    [InlineData("PropRectName", "Hit")]
    [InlineData("PropCircleName", "Ball")]
    [InlineData("PropPolygonName", "Hull")]
    public async Task InspectorShapeName_IsRefused(string box, string kept)
    {
        var (editor, path, walk) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(walk.Frames[0].ShapesSave!.Shapes.OfType<ShapeSave>().First(shape => shape.Name == kept));

        ShouldBeRefused(editor, path, editor.Control<TextBox>(box), kept);
    }

    [AvaloniaFact]
    public async Task InspectorNameOfShapesOfDifferentTypes_IsRefused()
    {
        var (editor, path, walk) = await OpenAsync();
        using var _ = editor;
        // Two shapes of one frame cannot share a name, so the box takes text only across frames.
        AnimationFrameSave copy = editor.Services.AppCommands.DuplicateFrame(walk.Frames[0], walk)!;
        editor.Expand(copy);
        int undoCount = editor.UndoLabels.Count;
        editor.ClickRow(walk.Frames[0].ShapesSave!.AARectSaves.Single());
        editor.ClickRow(copy.ShapesSave!.CircleSaves.Single(), RawInputModifiers.Control);
        TextBox box = editor.Control<TextBox>("PropShapesName");
        box.Focus();
        editor.Layout();
        box.Text = Bad;
        editor.Press(Key.Enter);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("U+0001");
        editor.UndoLabels.Count.ShouldBe(undoCount);
    }

    [AvaloniaTheory]
    [InlineData(true, "Step")]
    [InlineData(false, "left")]
    public async Task InspectorEvent_IsRefused(bool name, string kept)
    {
        var (editor, path, walk) = await OpenAsync();
        using var _ = editor;
        editor.ClickRow(walk.Frames[0]);
        var row = editor.Control<FrameEventsEditor>("PropFrameEvents").Rows[0];

        ShouldBeRefused(editor, path, name ? row.Name : row.Data, kept);
    }

    [AvaloniaFact]
    public async Task TreeRename_IsRefused()
    {
        var (editor, _, walk) = await OpenAsync();
        using var _ = editor;

        editor.Window.CommitInlineRenamePublic(editor.NodeFor(walk), Bad);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("U+0001");
        editor.UndoLabels.ShouldBeEmpty();
        walk.Name.ShouldBe("Walk");
    }

    [AvaloniaFact]
    public async Task ShapeLoadedWithSuchAName_KeepsItsOtherFieldsEditable()
    {
        var (editor, _, walk) = await OpenAsync("achj", Bad);
        using var _ = editor;
        AARectSave rect = walk.Frames[0].ShapesSave!.AARectSaves.Single();
        editor.ClickRow(rect);

        editor.TypeNumber("PropRectX", "7");

        rect.X.ShouldBe(7);
        rect.Name.ShouldBe(Bad);
        editor.ProblemText.ShouldBeNull();
    }
}
