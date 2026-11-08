using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A name box refuses the editor's own copy JSON: pasting it inserts nothing, ordinary text still pastes, and Ctrl+V
/// and the box's own Paste (the context menu's route) behave the same. One scenario per box, so a new name box that is
/// missing from <see cref="BoxNames"/> is still swept by <see cref="EveryTextBoxInTheWindow_ExceptTheOptedOutOnes_RefusesTheJson"/>.
/// </summary>
public class NameBoxPasteScenarioTests
{
    public static IEnumerable<object[]> BoxNames() => new[]
    {
        "tree rename", "list rename", "track rename", "sequence inspector name", "track inspector name",
        "chain name", "texture name", "rect name", "circle name", "polygon name", "event name",
    }.Select(name => new object[] { name });

    private static string CopiedJson() =>
        SequenceClipboard.Write(new SequenceSave { Name = "Idle" }, @"C:\game");

    private static string WriteSeqjFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var attack = new SequenceSave { Name = "Attack" };
        attack.Tracks.Add(new SequenceTrackSave { Name = "Body" });
        attack.Tracks.Add(new SequenceTrackSave { Name = "Arms" });
        save.Sequences.Add(attack);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(path);
        return path;
    }

    private static string WriteAchxFixture(AnimationEditorHarness editor)
    {
        editor.WritePng("sheet.png", 64, 64);
        AnimationChainSave walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16));
        walk.Frames[0].ShapesSave = new ShapesSave();
        walk.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit", ScaleX = 4, ScaleY = 4 });
        walk.Frames[0].ShapesSave!.Add(new CircleSave { Name = "Orb", Radius = 4 });
        var polygon = new PolygonSave { Name = "Poly" };
        polygon.Points.Add(new Vector2Save { X = 0, Y = 0 });
        polygon.Points.Add(new Vector2Save { X = 8, Y = 0 });
        polygon.Points.Add(new Vector2Save { X = 8, Y = 8 });
        walk.Frames[0].ShapesSave!.Add(polygon);
        return editor.WriteAchx("hero.achx", walk);
    }

    private static void Select(AnimationEditorHarness editor, object rowData)
    {
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.Expand(walk.Frames[0]);
        editor.ClickRow(rowData);
    }

    /// <summary>Opens the named box the way a user reaches it and returns it with its starting text.</summary>
    private static async Task<(TextBox Box, string Text)> OpenAsync(AnimationEditorHarness editor, string name)
    {
        TextBox box;
        switch (name)
        {
            case "tree rename":
                await editor.OpenAsync(WriteAchxFixture(editor));
                editor.ClickRow(editor.ChainNamed("Walk"));
                editor.Press(Key.F2);
                editor.Wait(TimeSpan.FromMilliseconds(50));
                box = editor.InlineRenameBox(editor.ChainNamed("Walk"));
                break;
            case "list rename":
                await editor.OpenAsync(WriteSeqjFixture(editor));
                var list = editor.Control<ListBox>("SequenceList");
                editor.DoubleClickAt(editor.CenterOf((Control)list.ContainerFromIndex(1)!));
                box = list.ContainerFromIndex(1)!.GetVisualDescendants().OfType<InlineRenameBox>().Single();
                break;
            case "track rename":
                await editor.OpenAsync(WriteSeqjFixture(editor));
                var timeline = editor.Control<SequenceTimelineControl>("SequenceTimeline");
                editor.DoubleClickAt(editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(1) + 10));
                box = timeline.GetVisualChildren().OfType<InlineRenameBox>().Single();
                break;
            case "sequence inspector name":
                await editor.OpenAsync(WriteSeqjFixture(editor));
                box = editor.Control<SequenceInspectorControl>("SequenceInspector").SequenceNameBox;
                break;
            case "track inspector name":
                await editor.OpenAsync(WriteSeqjFixture(editor));
                var tl = editor.Control<SequenceTimelineControl>("SequenceTimeline");
                editor.ClickAt(editor.PointIn(tl, 20, tl.Viewport.LaneTop(0) + 10));
                box = editor.Control<SequenceInspectorControl>("SequenceInspector").TrackNameBox;
                break;
            case "chain name":
                await editor.OpenAsync(WriteAchxFixture(editor));
                editor.ClickRow(editor.ChainNamed("Walk"));
                box = editor.Control<TextBox>("PropChainName");
                break;
            case "texture name":
                await editor.OpenAsync(WriteAchxFixture(editor));
                Select(editor, editor.ChainNamed("Walk").Frames[0]);
                box = editor.Control<TextBox>("PropTextureName");
                break;
            case "rect name":
                await editor.OpenAsync(WriteAchxFixture(editor));
                Select(editor, editor.ChainNamed("Walk").Frames[0].ShapesSave!.AARectSaves.Single());
                box = editor.Control<TextBox>("PropRectName");
                break;
            case "circle name":
                await editor.OpenAsync(WriteAchxFixture(editor));
                Select(editor, editor.ChainNamed("Walk").Frames[0].ShapesSave!.CircleSaves.Single());
                box = editor.Control<TextBox>("PropCircleName");
                break;
            case "polygon name":
                await editor.OpenAsync(WriteAchxFixture(editor));
                Select(editor, editor.ChainNamed("Walk").Frames[0].ShapesSave!.PolygonSaves.Single());
                box = editor.Control<TextBox>("PropPolygonName");
                break;
            case "event name":
                await editor.OpenAsync(WriteAchxFixture(editor));
                Select(editor, editor.ChainNamed("Walk").Frames[0]);
                var events = editor.Control<FrameEventsEditor>("PropFrameEvents");
                editor.ScrollIntoView(events);
                editor.Click(events.AddButton);
                box = events.Rows[0].Name;
                break;
            default:
                throw new ArgumentException(name);
        }
        box.Focus();
        editor.Layout();
        return (box, box.Text ?? "");
    }

    private static async Task PasteAsync(AnimationEditorHarness editor, TextBox box, string clipboard, bool throughPasteMethod)
    {
        await editor.Window.Clipboard!.SetTextAsync(clipboard);
        box.Focus();
        editor.Layout();
        box.SelectAll();
        if (throughPasteMethod) box.Paste();
        else editor.Press(Key.V, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));
        editor.Layout();
    }

    [AvaloniaTheory]
    [MemberData(nameof(BoxNames))]
    public async Task CtrlV_WithTheEditorsCopyJson_InsertsNothing(string name)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (box, before) = await OpenAsync(editor, name);

        await PasteAsync(editor, box, CopiedJson(), throughPasteMethod: false);

        box.Text.ShouldBe(before);
    }

    [AvaloniaTheory]
    [MemberData(nameof(BoxNames))]
    public async Task TheBoxsOwnPaste_WithTheEditorsCopyJson_InsertsNothing(string name)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (box, before) = await OpenAsync(editor, name);

        await PasteAsync(editor, box, CopiedJson(), throughPasteMethod: true);

        box.Text.ShouldBe(before);
    }

    [AvaloniaTheory]
    [MemberData(nameof(BoxNames))]
    public async Task CtrlV_WithOrdinaryText_InsertsIt(string name)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (box, _) = await OpenAsync(editor, name);

        await PasteAsync(editor, box, "hello", throughPasteMethod: false);

        box.Text.ShouldBe("hello");
    }

    [AvaloniaTheory]
    [MemberData(nameof(BoxNames))]
    public async Task TheBoxsOwnPaste_WithOrdinaryText_InsertsIt(string name)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (box, _) = await OpenAsync(editor, name);

        await PasteAsync(editor, box, "hello", throughPasteMethod: true);

        box.Text.ShouldBe("hello");
    }

    [AvaloniaFact]
    public async Task EveryKindOfCopyJson_IsRefused()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (box, before) = await OpenAsync(editor, "sequence inspector name");
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk" });
        string[] copies =
        {
            TimelineClipboard.WriteTracks(new[] { track }, @"C:\game"),
            TimelineClipboard.WriteChains(new[] { new CopiedChain(track.Chains[0], track, 0) }, @"C:\game"),
            ClipboardPayload.Serialize(new List<AnimationChainSave> { new() { Name = "Walk" } }),
            ClipboardPayload.Serialize(new List<AnimationFrameSave> { new() { TextureName = "a.png" } }),
            ClipboardPayload.Serialize(new AARectSave { Name = "Box" }),
            "{ \"kind\": \"frb2-animation-editor-sequence\" }",
        };

        foreach (string copy in copies)
        {
            await PasteAsync(editor, box, copy, throughPasteMethod: false);
            box.Text.ShouldBe(before);
        }
    }

    [AvaloniaFact]
    public async Task CtrlCXV_InANameBox_ActOnTheTextOnly()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (box, _) = await OpenAsync(editor, "chain name");
        await editor.Window.Clipboard!.SetTextAsync(CopiedJson());
        int chains = editor.Project.AnimationChains.Count;
        box.Focus();
        editor.Layout();

        box.Text = "Walk";
        box.SelectAll();
        editor.Press(Key.C, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        (await editor.Window.Clipboard!.TryGetTextAsync()).ShouldBe("Walk");

        editor.Press(Key.X, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        box.Text.ShouldBe("");
        (await editor.Window.Clipboard!.TryGetTextAsync()).ShouldBe("Walk");

        editor.Press(Key.V, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        box.Text.ShouldBe("Walk");
        editor.Project.AnimationChains.Count.ShouldBe(chains);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task EveryTextBoxInTheWindow_ExceptTheOptedOutOnes_RefusesTheJson()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteAchxFixture(editor));
        await editor.Window.Clipboard!.SetTextAsync(CopiedJson());

        // A TextBox that takes the editor's JSON on purpose must say so (EditorTextPaste.AllowEditorJson); every other one
        // refuses it with no per-box wiring, so a box added later is covered without being listed in BoxNames.
        var boxes = editor.Window.GetVisualDescendants().OfType<TextBox>().Where(box => !box.IsReadOnly).ToList();
        boxes.Count.ShouldBeGreaterThan(5);
        var optedOut = boxes.Where(EditorTextPaste.GetAllowEditorJson).Select(box => box.Name ?? box.GetType().Name).Distinct().ToList();
        optedOut.ShouldBe(new[] { "SearchBox" });
        foreach (TextBox box in boxes.Where(box => !EditorTextPaste.GetAllowEditorJson(box)))
        {
            box.Text = "x";
            box.SelectAll();
            box.Paste();
            await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
            box.Text.ShouldBe("x", $"{box.Name ?? box.GetType().Name} took the pasted JSON");
        }
    }
}
