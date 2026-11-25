using AnimationEditor.Core.Tiled;
using AnimationEditor.Editing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>What a <c>.tsx</c> cannot store is refused where the user asks for it (#2009).</summary>
public class TsxRefusedEditsScenarioTests
{
    private const string TsxXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <properties>
           <property name="Name" value="A"/>
          </properties>
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
         <tile id="4">
          <properties>
           <property name="Name" value="B"/>
          </properties>
          <animation>
           <frame tileid="4" duration="200"/>
           <frame tileid="5" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static async Task<AnimationChainSave> OpenTsxAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("Heroes.png", 64, 64);
        string path = Path.Combine(editor.ProjectFolder, "Heroes.tsx");
        File.WriteAllText(path, TsxXml);
        await editor.OpenAsync(path);
        AnimationChainSave chain = editor.ChainNamed("A");
        editor.Expand(chain);
        return chain;
    }

    private static async Task<AnimationChainSave> OpenAchxAsync(AnimationEditorHarness editor)
    {
        editor.WritePng("Heroes.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("A", "Heroes.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "Heroes.png", (0, 16, 16, 16), (16, 16, 16, 16))));
        AnimationChainSave chain = editor.ChainNamed("A");
        editor.Expand(chain);
        return chain;
    }

    public enum Grab { Frame, SeveralFrames, Animation, SeveralAnimations, AnimationAndFrame }

    /// <summary>Every sprite drag of the preview. The .achx rows prove each gesture really drags.</summary>
    [AvaloniaTheory]
    [InlineData(Grab.Frame, true), InlineData(Grab.Frame, false)]
    [InlineData(Grab.SeveralFrames, true), InlineData(Grab.SeveralFrames, false)]
    [InlineData(Grab.Animation, true), InlineData(Grab.Animation, false)]
    [InlineData(Grab.SeveralAnimations, true), InlineData(Grab.SeveralAnimations, false)]
    [InlineData(Grab.AnimationAndFrame, true), InlineData(Grab.AnimationAndFrame, false)]
    public async Task PreviewSpriteDrag_InATsx_MovesNothingAndShowsNoMoveCursor(Grab grab, bool tsx)
    {
        using var editor = new AnimationEditorHarness();
        AnimationChainSave a = tsx ? await OpenTsxAsync(editor) : await OpenAchxAsync(editor);
        AnimationChainSave b = editor.ChainNamed("B");
        editor.ClickRow(grab is Grab.Frame or Grab.SeveralFrames ? a.Frames[0] : a);
        if (grab == Grab.SeveralFrames) editor.ClickRow(a.Frames[1], RawInputModifiers.Control);
        if (grab == Grab.SeveralAnimations) editor.ClickRow(b, RawInputModifiers.Control);
        if (grab == Grab.AnimationAndFrame)
        {
            editor.Expand(b);
            editor.ClickRow(b.Frames[0], RawInputModifiers.Control);
        }
        // Clear of the group timeline, which docks over the preview's lower half.
        editor.MiddleDrag(editor.PreviewPointAt(0, 40), editor.PreviewPointAt(0, 80));
        Point from = editor.PreviewPointAt(0, 0);
        Point local = from - editor.PointIn(editor.Preview, 0, 0);

        var cursor = editor.Preview.GetHoverCursorTypeForTest((float)local.X, (float)local.Y);
        editor.Drag(from, editor.PreviewPointAt(10, 0));

        cursor.ShouldBe(tsx ? null : StandardCursorType.SizeAll);
        editor.Project.AnimationChains.SelectMany(c => c.Frames).Any(f => f.RelativeX != 0).ShouldBe(!tsx);
        editor.UndoLabels.Count.ShouldBe(tsx ? 0 : 1);
        if (tsx)
            editor.Project.AnimationChains.SelectMany(c => TsxLossyDataCheck.DroppedKinds(c, editor.Project.TimeMeasurementUnit)).ShouldBeEmpty();
    }

    private static Task<bool> DropPng(AnimationEditorHarness editor, AnimationChainSave chain, AnimationFrameSave? frame, string path, bool ctrl) =>
        (Task<bool>)typeof(MainWindow).GetMethod("HandlePngDropAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(editor.Window, [chain, frame, path, ctrl])!;

    /// <summary>The image outside the folder would ask to be copied next to the file first.</summary>
    [AvaloniaTheory]
    [InlineData(true, false, "other.png")]
    [InlineData(false, false, "other.png")]
    [InlineData(false, true, "other.png")]
    [InlineData(true, false, "../other.png")]
    public async Task DroppingAnotherImage_InATsx_IsRefusedWithTheReason(bool onFrame, bool ctrl, string image)
    {
        using var editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        string sub = Path.Combine(editor.ProjectFolder, "sub");
        Directory.CreateDirectory(sub);
        string other = editor.WritePng("other.png", 64, 64);
        if (image.StartsWith(".."))
        {
            string moved = Path.Combine(sub, "Heroes.tsx");
            File.WriteAllText(moved, TsxXml.Replace("Heroes.png", "../Heroes.png"));
            await editor.OpenAsync(moved);
            chain = editor.ChainNamed("A");
        }

        (await DropPng(editor, chain, onFrame ? chain.Frames[0] : null, other, ctrl)).ShouldBeFalse();

        editor.ProblemText.ShouldNotBeNull().ShouldContain(TsxEdit.OtherImageRefusal);
        chain.Frames.Count.ShouldBe(2);
        chain.Frames.ShouldAllBe(f => f.TextureName.EndsWith("Heroes.png"));
        editor.UndoLabels.ShouldBeEmpty();
        File.Exists(Path.Combine(sub, "other.png")).ShouldBeFalse();
    }

    /// <summary>The tileset's image is one folder up, where another file would be offered for copying.</summary>
    [AvaloniaFact]
    public async Task DroppingTheTilesetsOwnImage_InATsx_IsAllowedWhereItIs()
    {
        using var editor = new AnimationEditorHarness();
        string own = editor.WritePng("Heroes.png", 64, 64);
        string sub = Path.Combine(editor.ProjectFolder, "sub");
        Directory.CreateDirectory(sub);
        string path = Path.Combine(sub, "Heroes.tsx");
        File.WriteAllText(path, TsxXml.Replace("Heroes.png", "../Heroes.png"));
        await editor.OpenAsync(path);
        AnimationChainSave chain = editor.ChainNamed("A");

        (await DropPng(editor, chain, chain.Frames[0], own, ctrl: false)).ShouldBeTrue();

        editor.ProblemText.ShouldBeNull();
        chain.Frames[0].TextureName.ShouldBe("../Heroes.png");
        File.Exists(Path.Combine(sub, "Heroes.png")).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task InspectorTexture_IsDisabledInATsxOnly()
    {
        using var editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        editor.ClickRow(chain.Frames[0]);
        editor.Control<TextBox>("PropTextureName").IsEnabled.ShouldBeFalse();
        editor.Control<Button>("PropTextureBrowseBtn").IsEnabled.ShouldBeFalse();

        chain = await OpenAchxAsync(editor);
        editor.ClickRow(chain.Frames[0]);
        editor.Control<TextBox>("PropTextureName").IsEnabled.ShouldBeTrue();
        editor.Control<Button>("PropTextureBrowseBtn").IsEnabled.ShouldBeTrue();
    }

    /// <summary>A frame with another image (pasted here) puts that image in the texture list and on the canvas.</summary>
    [AvaloniaFact]
    public async Task PickingAnotherImageInTheTextureList_InATsx_IsRefusedForAFrameAndForANewRegion()
    {
        using var editor = new AnimationEditorHarness();
        AnimationChainSave chain = await OpenTsxAsync(editor);
        string other = editor.WritePng("other.png", 64, 64);
        editor.ChainNamed("B").Frames[0].TextureName = "other.png";
        editor.Services.ApplicationEvents.RaiseAnimationChainsChanged();
        editor.ClickRow(chain.Frames[0]);
        var combo = editor.Control<ComboBox>("TextureCombo");
        object? own = combo.SelectedItem;
        own.ShouldNotBeNull();
        combo.Items.Add(other.Replace('\\', '/'));

        combo.SelectedItem = other.Replace('\\', '/');
        editor.Layout();

        editor.ProblemText.ShouldNotBeNull().ShouldContain(TsxEdit.OtherImageRefusal);
        chain.Frames[0].TextureName.ShouldBe("Heroes.png");
        combo.SelectedItem.ShouldBe(own);

        editor.Notifications.ErrorBanner.IsVisible = false;
        editor.Wireframe.LoadTexture(other);
        typeof(MainWindow).GetMethod("OnFrameCreatedFromRegion", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(editor.Window, [0, 0, 16, 16]);

        editor.ProblemText.ShouldNotBeNull().ShouldContain(TsxEdit.OtherImageRefusal);
        chain.Frames.Count.ShouldBe(2);
    }

    [AvaloniaTheory]
    [InlineData(Key.C)]
    [InlineData(Key.X)]
    public async Task PastingAShapeOnATsxFrame_SaysWhyItDoesNothing(Key copyOrCut)
    {
        using var editor = new AnimationEditorHarness();
        AnimationChainSave source = await OpenAchxAsync(editor);
        (source.Frames[0].ShapesSave ??= new ShapesSave()).Add(new CircleSave { Name = "Hit", Radius = 4 });
        editor.Services.AppCommands.RefreshTreeNode(source.Frames[0]);
        editor.Expand(source.Frames[0]);
        editor.ClickRow(source.Frames[0].ShapesSave!.Shapes[0]);
        editor.Press(copyOrCut, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));
        AnimationChainSave chain = await OpenTsxAsync(editor);
        editor.ClickRow(chain.Frames[0]);

        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.ProblemText.ShouldNotBeNull().ShouldContain(TsxEdit.ShapesRefusal);
        chain.Frames.ShouldAllBe(f => f.ShapesSave == null || f.ShapesSave.Shapes.Count == 0);
        source.Frames[0].ShapesSave!.Shapes.Count.ShouldBe(1);
    }
}
