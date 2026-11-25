using AnimationEditor.App.Controls;
using AnimationEditor.Core.DragDrop;
using AnimationEditor.DocumentMatrix;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A frame of another size put into an animation of a <c>.tsx</c> is refused with the reason and stays where it is
/// (#2147). Blink is 16x16 (tiles 5, 6), Torch is 32x16 (tiles 8, 10).
/// </summary>
public class TsxFrameSizeScenarioTests
{
    private const string Reason = "All frames of an animation in a .tsx have one size, so a 16x16 frame cannot be in 'Torch' with a 32x16 frame.";

    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="5">
          <properties>
           <property name="Name" value="Blink"/>
          </properties>
          <animation>
           <frame tileid="5" duration="100"/>
           <frame tileid="6" duration="100"/>
          </animation>
         </tile>
         <tile id="8">
          <properties>
           <property name="Name" value="Torch"/>
          </properties>
          <animation>
           <frame tileid="8" duration="100"/>
           <frame tileid="10" duration="100"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="8"/>
          </properties>
          <animation>
           <frame tileid="9" duration="100"/>
           <frame tileid="11" duration="100"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static async Task<string> OpenTsxAsync(AnimationEditorHarness editor, string name = "Heroes.tsx")
    {
        editor.WritePng("Heroes.png", 64, 64);
        string path = Path.Combine(editor.ProjectFolder, name);
        File.WriteAllText(path, TsxXml);
        await editor.OpenAsync(path);
        return path;
    }

    private static void Press(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
    }

    private static void ClickFrame(AnimationEditorHarness editor, string chain)
    {
        editor.Expand(editor.ChainNamed(chain));
        editor.ClickRow(editor.ChainNamed(chain).Frames[0]);
    }

    private static void ShouldBeRefused(AnimationEditorHarness editor, string path)
    {
        editor.ProblemText.ShouldNotBeNull().ShouldContain(Reason);
        editor.ChainNamed("Torch").Frames.Count.ShouldBe(2);
        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllText(path).ShouldBe(TsxXml);
    }

    [AvaloniaTheory]
    [InlineData(Key.C)]
    [InlineData(Key.X)]
    public async Task FramePastedInTheSameFile_IsRefused_AndACutStaysPending(Key copyOrCut)
    {
        using var editor = new AnimationEditorHarness();
        string path = await OpenTsxAsync(editor);
        ClickFrame(editor, "Blink");
        Press(editor, copyOrCut);
        editor.ClickRow(editor.ChainNamed("Torch"));

        Press(editor, Key.V);

        ShouldBeRefused(editor, path);
        editor.ChainNamed("Blink").Frames.Count.ShouldBe(2);

        editor.ClickRow(editor.ChainNamed("Blink"));
        Press(editor, Key.V);

        Documents.TileAnimations(path).ShouldContain(copyOrCut == Key.X ? "5: 6@100 5@100" : "5: 5@100 6@100 5@100");
    }

    [AvaloniaTheory]
    [InlineData(Key.C, true), InlineData(Key.X, true)]
    [InlineData(Key.C, false), InlineData(Key.X, false)]
    public async Task FramePastedFromAnotherTab_IsRefused_AndTheOtherFileKeepsIt(Key copyOrCut, bool fromTsx)
    {
        using var editor = new AnimationEditorHarness();
        string other;
        if (fromTsx)
        {
            other = await OpenTsxAsync(editor, "Other.tsx");
        }
        else
        {
            editor.WritePng("Heroes.png", 64, 64);
            other = editor.WriteAchx("Other.achx", AnimationEditorHarness.Chain("Blink", "Heroes.png", (16, 16, 16, 16), (32, 16, 16, 16)));
            await editor.OpenAsync(other);
        }
        string otherBefore = File.ReadAllText(other);
        ClickFrame(editor, "Blink");
        Press(editor, copyOrCut);
        string path = await OpenTsxAsync(editor);
        editor.ClickRow(editor.ChainNamed("Torch"));

        Press(editor, Key.V);

        ShouldBeRefused(editor, path);
        File.ReadAllText(other).ShouldBe(otherBefore);
    }

    /// <summary>A whole animation arrives as it was copied; the save says it was not stored, and the cut keeps its source.</summary>
    [AvaloniaFact]
    public async Task AnimationOfTwoSizesCutFromAnotherTab_StaysInItsFile()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("Heroes.png", 64, 64);
        string other = editor.WriteAchx("Other.achx", AnimationEditorHarness.Chain("Mixed", "Heroes.png", (0, 0, 16, 16), (0, 0, 32, 16)));
        await editor.OpenAsync(other);
        string otherBefore = File.ReadAllText(other);
        editor.ClickRow(editor.ChainNamed("Mixed"));
        Press(editor, Key.X);
        string path = await OpenTsxAsync(editor);

        Press(editor, Key.V);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("Mixed");
        File.ReadAllText(other).ShouldBe(otherBefore);
        Documents.TileAnimations(path).Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task FrameDraggedOntoAnotherAnimation_ShowsNoDropLine_AndIsRefused()
    {
        using var editor = new AnimationEditorHarness();
        string path = await OpenTsxAsync(editor);
        var blink = editor.ChainNamed("Blink");
        typeof(MainWindow).GetField("_pendingFrameDrag", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(editor.Window, new FrameDragSource(new[] { blink.Frames[0] }, blink, FrameDragValidity.Valid));

        Raise(editor, DragDrop.DragOverEvent, editor.ChainNamed("Torch"));
        editor.Control<Canvas>("DragOverlayCanvas").Children.ShouldBeEmpty();
        Raise(editor, DragDrop.DragOverEvent, blink);
        editor.Control<Canvas>("DragOverlayCanvas").Children.Count.ShouldBe(1);
        Raise(editor, DragDrop.DropEvent, editor.ChainNamed("Torch"));

        ShouldBeRefused(editor, path);
        blink.Frames.Count.ShouldBe(2);
    }

    /// <summary>Blink keeps tile 5, so an animation that starts with its first frame cannot be saved (#2152).</summary>
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FirstFrameMovedIntoAnEmptyAnimation_IsRefused_UntilItsTileIsFree(bool drag)
    {
        const string reason = "'New' could not be saved with the frame, so the frame would be in no animation of the file: "
            + "Tiled tile 5 already carries \"Blink\". Give \"Blink\" another Placed Tile first.";
        using var editor = new AnimationEditorHarness();
        string path = await OpenTsxAsync(editor);
        var blink = editor.ChainNamed("Blink");
        var empty = editor.Services.AppCommands.AddAnimationChainWithName("New")!;
        editor.Layout();
        if (drag)
        {
            typeof(MainWindow).GetField("_pendingFrameDrag", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(editor.Window, new FrameDragSource(new[] { blink.Frames[0] }, blink, FrameDragValidity.Valid));
        }
        else
        {
            ClickFrame(editor, "Blink");
            Press(editor, Key.X);
            editor.ClickRow(empty);
        }

        Put();

        editor.ProblemText.ShouldNotBeNull().ShouldContain(reason);
        empty.Frames.ShouldBeEmpty();
        Documents.TileAnimations(path).ShouldContain("5: 5@100 6@100");

        editor.Services.AppCommands.SetChainTsxOwnerTileId(blink, 1).ShouldBeNull();
        if (!drag) editor.ClickRow(empty);
        Put();

        Documents.TileAnimations(path).Where(t => t.StartsWith("1:") || t.StartsWith("5:")).ShouldBe(["1: 6@100", "5: 5@100"]);

        void Put()
        {
            if (drag) Raise(editor, DragDrop.DropEvent, empty);
            else Press(editor, Key.V);
        }
    }

    // The OS drag cannot run headless.
    private static void Raise(AnimationEditorHarness editor, RoutedEvent<DragEventArgs> dragEvent, AnimationChainSave target)
    {
        var format = typeof(MainWindow).GetField("FrameDragDataFormat", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create((DataFormat<string>)format, "frame"));
        editor.Layout();
        Control row = editor.RowFor(target);
        Point point = row.TranslatePoint(new Point(row.Bounds.Width / 2, 8), editor.AnimTree)!.Value;
        editor.AnimTree.RaiseEvent(new DragEventArgs(dragEvent, data, editor.AnimTree, point, KeyModifiers.None));
        editor.Layout();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegionOnTheTexture_OfAnotherSize_AddsNoFrame(bool bothAnimations)
    {
        using var editor = new AnimationEditorHarness();
        string path = await OpenTsxAsync(editor);
        editor.ClickRow(editor.ChainNamed("Torch"));
        if (bothAnimations) editor.ClickRow(editor.ChainNamed("Blink"), RawInputModifiers.Control);

        typeof(MainWindow).GetMethod("OnFrameCreatedFromRegion", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(editor.Window, [0, 0, 16, 16]);

        ShouldBeRefused(editor, path);
        editor.ChainNamed("Blink").Frames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task RegionOnTheTexture_OfTheSizeOfTheUnlockedAnimation_IsAddedToIt()
    {
        using var editor = new AnimationEditorHarness();
        await OpenTsxAsync(editor);
        editor.ChainNamed("Torch").IsLocked = true;
        editor.ClickRow(editor.ChainNamed("Torch"));
        editor.ClickRow(editor.ChainNamed("Blink"), RawInputModifiers.Control);

        typeof(MainWindow).GetMethod("OnFrameCreatedFromRegion", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(editor.Window, [0, 0, 16, 16]);

        editor.ChainNamed("Blink").Frames.Count.ShouldBe(3);
        editor.ChainNamed("Torch").Frames.Count.ShouldBe(2);
    }
}
