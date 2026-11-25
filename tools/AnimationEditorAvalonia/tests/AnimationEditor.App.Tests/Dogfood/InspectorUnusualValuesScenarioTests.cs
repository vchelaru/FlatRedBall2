using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Inspector fields over values outside their range, frames with no texture, and a selection that is partly locked.</summary>
public class InspectorUnusualValuesScenarioTests
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

    [AvaloniaFact]
    public async Task RectName_FocusInAndOut_ChangesNothing_WhenASizeIsZero()
    {
        using var editor = await Open(chain => AddRect(chain.Frames[0], "A", 0, 0, 0, 4));
        var rect = editor.ChainNamed("Walk").Frames[0].ShapesSave!.AARectSaves.First();
        editor.ClickRow(rect);

        editor.Control<TextBox>("PropRectName").Focus();
        editor.ClickRow(editor.ChainNamed("Walk").Frames[1]);

        editor.UndoLabels.ShouldBeEmpty();
        rect.ScaleX.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task RectName_Rename_LeavesOffsetAndSizeAlone()
    {
        using var editor = await Open(chain => AddRect(chain.Frames[0], "A", 2, 20000, -5, 4));
        var rect = editor.ChainNamed("Walk").Frames[0].ShapesSave!.AARectSaves.First();
        editor.ClickRow(rect);

        editor.TypeText("PropRectName", "B");

        rect.Name.ShouldBe("B");
        rect.X.ShouldBe(2);
        rect.Y.ShouldBe(20000);
        rect.ScaleX.ShouldBe(-5);
    }

    [AvaloniaFact]
    public async Task Select_AFrameWithAnOffsetNoDecimalHolds_DoesNotThrow()
    {
        using var editor = await Open(chain =>
        {
            chain.Frames[0].RelativeX = 1e30f;
            AddRect(chain.Frames[1], "A", float.MaxValue, 0, 4, 4);
        });
        var walk = editor.ChainNamed("Walk");

        Should.NotThrow(() => editor.ClickRow(walk.Frames[0]));
        Should.NotThrow(() => editor.ClickRow(walk.Frames[1].ShapesSave!.AARectSaves.First()));
    }

    [AvaloniaFact]
    public async Task PixelFields_OfAFrameWithNoTexture_DoNotShowAnotherFramesRegion()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var bare = AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 20, 24));
        bare.Frames[0].TextureName = "";
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)), bare);
        await editor.OpenAsync(path);
        editor.Expand(editor.ChainNamed("Walk"));
        editor.Expand(editor.ChainNamed("Run"));
        editor.ClickRow(editor.ChainNamed("Walk").Frames[0]);
        editor.ClickRow(editor.ChainNamed("Run").Frames[0]);

        editor.Control<NumericUpDown>("PropPixelX").Value.ShouldNotBe(8);
        editor.Control<NumericUpDown>("PropPixelW").Value.ShouldNotBe(16);
    }

    [AvaloniaFact]
    public async Task AllFields_LockedAndUnlockedSelection_LeaveTheLockedFrameAlone_InOneUndoEntryEach()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        var locked = AnimationEditorHarness.Chain("Locked", "sheet.png", (0, 0, 16, 16));
        locked.IsLocked = true;
        string path = editor.WriteAchx("hero.achx", locked,
            AnimationEditorHarness.Chain("Walk", "sheet.png", (16, 0, 16, 16), (32, 0, 8, 8)));
        await editor.OpenAsync(path);
        var walk = editor.ChainNamed("Walk");
        var held = editor.ChainNamed("Locked").Frames[0];
        editor.Expand(editor.ChainNamed("Locked"));
        editor.Expand(walk);
        editor.ClickRow(held);
        editor.ClickRow(walk.Frames[0], RawInputModifiers.Control);
        editor.ClickRow(walk.Frames[1], RawInputModifiers.Control);
        string before = AnimationEditorHarness.ReadSaved(path).ToJsonString();

        editor.TypeNumber("PropPixelX", "+ 2");
        editor.TypeNumber("PropPixelW", "* 2");
        editor.TypeNumber("PropRelX", "3");
        editor.TypeFlanker("PropFrameLen", "0.3");
        editor.TypeNumber("PropRed", "20");
        editor.TypeNumber("PropAlpha", "30");
        editor.Click(editor.Control<Control>("PropFlipH"));

        (held.LeftCoordinate, held.RightCoordinate, held.RelativeX, held.Red, held.Alpha, held.FlipHorizontal).ShouldBe((0f, 16f, 0f, null, null, false));
        walk.Frames.Select(f => (f.LeftCoordinate, f.RightCoordinate)).ShouldBe(new[] { (18f, 50f), (34f, 50f) });
        walk.Frames.ShouldAllBe(f => f.Red == 20 && f.Alpha == 30 && f.FlipHorizontal && f.FrameLength == 0.3f);
        editor.UndoLabels.Count.ShouldBe(7, string.Join(" | ", editor.UndoLabels));
        while (editor.UndoManager.CanUndo) editor.UndoManager.Undo();
        editor.Layout();
        AnimationEditorHarness.ReadSaved(path).ToJsonString().ShouldBe(before);
    }
}
