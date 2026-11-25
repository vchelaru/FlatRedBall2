using AnimationEditor.App.Controls;
using AnimationEditor.Views.Dialogs;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Leaving a document in any way commits what is typed in the Inspector field being edited, also in one that otherwise
/// commits only on Enter or when it loses the focus (<see cref="AchxRenameLeaveScenarioTests"/> covers the name boxes).
/// </summary>
public class TypedFieldLeaveScenarioTests
{
    private static string WriteFixture(AnimationEditorHarness editor, string name = "hero.achx")
    {
        editor.WritePng("sheet.png", 64, 64);
        var walk = AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16));
        (walk.Frames[0].FrameLength, walk.Frames[1].FrameLength) = (0.1f, 0.2f);
        (walk.Frames[0].Red, walk.Frames[1].Red) = (10, 20);
        (walk.Frames[0].RelativeX, walk.Frames[1].RelativeX) = (1, 2);
        walk.Frames[0].Events.Add(new AnimationFrameEvent { Name = "Step", Data = "left" });
        return editor.WriteAchx(name, walk);
    }

    private static List<AnimationFrameSave> Saved(string path) =>
        AnimationEditorHarness.ReadSaved(path).AnimationChains[0].Frames;

    // Selects the first frame (or both) and types text in a field, without Enter.
    private static void Begin(AnimationEditorHarness editor, string field, string text, bool both = false)
    {
        // Opening a project folder brings the Animations sidebar tab to the front.
        editor.Window.SidebarTabs.SelectedItem = editor.Control<TabItem>("InspectorTab");
        editor.Layout();
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        if (both) editor.ClickRow(walk.Frames[1], RawInputModifiers.Control);
        TextBox box = field switch
        {
            "EventName" => editor.Control<FrameEventsEditor>("PropFrameEvents").Rows[0].Name,
            "EventData" => editor.Control<FrameEventsEditor>("PropFrameEvents").Rows[0].Data,
            _ => editor.Control<Control>(field).GetVisualDescendants().OfType<TextBox>().First(),
        };
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(text);
    }

    private static TreeViewItem ProjectRow(AnimationEditorHarness editor, string name) =>
        editor.Control<ProjectPanelControl>("ProjectPanel").GetVisualDescendants().OfType<TreeViewItem>()
            .Single(item => item.DataContext is AchxTreeNodeVm vm && vm.Name == name);

    private static async Task Leave(AnimationEditorHarness editor, string way, string hero)
    {
        switch (way)
        {
            case "achx tab": editor.ClickTab("other.achx"); break;
            case "seqj tab": editor.ClickTab("boss.seqj"); break;
            case "png tab": await editor.OpenPngTabAsync(Path.Combine(editor.ProjectFolder, "sheet.png")); break;
            case "open": await editor.OpenAsync(editor.WriteAchx("third.achx", AnimationEditorHarness.Chain("Z", "sheet.png", (0, 0, 8, 8)))); break;
            case "new": editor.Dialogs.AnswerNextSaveFile(null); editor.Press(Key.N, RawInputModifiers.Control); break;
            case "close tab": editor.CloseTab("hero.achx"); break;
            case "close project": editor.Window.CloseProjectFolder(); break;
            case "close window": editor.Window.Close(); break;
            case "Ctrl+S": editor.Press(Key.S, RawInputModifiers.Control); break;
            case "mouse back":
                editor.Window.MouseDown(new Point(300, 300), MouseButton.XButton1);
                editor.Window.MouseUp(new Point(300, 300), MouseButton.XButton1);
                break;
        }
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
    }

    [AvaloniaTheory]
    [InlineData("achx tab")]
    [InlineData("seqj tab")]
    [InlineData("png tab")]
    [InlineData("open")]
    [InlineData("new")]
    [InlineData("close tab")]
    [InlineData("close project")]
    [InlineData("close window")]
    [InlineData("Ctrl+S")]
    [InlineData("mouse back")]
    public async Task LengthAndRed_AreCommitted_OnEveryWayOut(string way)
    {
        foreach (string field in new[] { "PropFrameLen", "PropRed" })
        {
            using var editor = new AnimationEditorHarness();
            string hero = WriteFixture(editor);
            string other = WriteFixture(editor, "other.achx");
            if (way == "close project") await editor.OpenProjectFolderAsync();
            await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "One"));
            await editor.OpenAsync(other);
            await editor.OpenAsync(hero);

            Begin(editor, field, field == "PropRed" ? "7" : "0.5");
            await Leave(editor, way, hero);

            var saved = Saved(hero);
            if (field == "PropRed") saved[0].Red.ShouldBe(7);
            else saved[0].FrameLength.ShouldBe(0.5f);
            (Saved(other)[0].FrameLength, Saved(other)[0].Red).ShouldBe((0.1f, 10));
        }
    }

    [AvaloniaFact]
    public async Task ReplacingAPreviewTab_CommitsTheTypedLength()
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        WriteFixture(editor, "other.achx");
        await editor.OpenProjectFolderAsync();
        editor.Layout();
        editor.Click(ProjectRow(editor, "hero.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));
        editor.Tabs.ActiveTab!.IsPreview.ShouldBeTrue();

        Begin(editor, "PropFrameLen", "0.5");
        editor.Window.SidebarTabs.SelectedItem = editor.Control<TabItem>("ProjectTab");
        editor.Layout();
        editor.Click(ProjectRow(editor, "other.achx"));
        editor.Wait(TimeSpan.FromMilliseconds(200));

        Saved(hero)[0].FrameLength.ShouldBe(0.5f);
    }

    [AvaloniaFact]
    public async Task ClosingAnUntitledTab_SavesTheTypedLength()
    {
        using var editor = new AnimationEditorHarness();
        editor.Dialogs.AnswerNextSaveFile(null);
        editor.ClickMenu("MenuNew");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Walk");
        editor.Press(Key.Enter);
        editor.Click(editor.RowButton(editor.ChainNamed("Walk"), "Add Frame"));
        string target = Path.Combine(editor.ProjectFolder, "new.achx");
        editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Save);
        editor.Dialogs.AnswerNextSaveFile(target);

        Begin(editor, "PropFrameLen", "0.5");
        var tab = editor.CenterOf(editor.TabFor(editor.TabLabels.Single()));
        editor.Window.MouseDown(tab, MouseButton.Middle); // takes no focus, as the close button does
        editor.Window.MouseUp(tab, MouseButton.Middle);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        Saved(target)[0].FrameLength.ShouldBe(0.5f);
    }

    [AvaloniaTheory]
    [InlineData("PropGreen", "7", false, "Set Frame Color")]
    [InlineData("PropAlpha", "7", false, "Set Frame Alpha")]
    [InlineData("PropRed", "+ 5", true, "Set Frame Color")]
    [InlineData("PropRelX", "+ 4", true, "Set Offset")]
    [InlineData("PropFrameLen", "+ 1", true, "Set Length")]
    [InlineData("EventName", "Footstep", false, null)]
    [InlineData("EventData", "right", false, null)]
    public async Task EveryFieldThatCommitsWhenLeft_IsCommittedOnceByATabClick(string field, string text, bool both, string? label)
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        string otherBefore = File.ReadAllText(other);
        await editor.OpenAsync(other);
        editor.Expand(editor.ChainNamed("Walk"));
        editor.ClickRow(editor.ChainNamed("Walk").Frames[0]);
        await editor.OpenAsync(hero);

        Begin(editor, field, text, both);
        editor.ClickTab("other.achx");
        editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.UndoLabels.Count.ShouldBe(1);
        if (label is not null) editor.UndoLabels[0].ShouldBe(label);
        File.ReadAllText(other).ShouldBe(otherBefore);
        var frames = Saved(hero);
        switch (field)
        {
            case "PropGreen": frames[0].Green.ShouldBe(7); break;
            case "PropAlpha": frames[0].Alpha.ShouldBe(7); break;
            case "PropRed": frames.Select(f => f.Red).ShouldBe(new int?[] { 15, 25 }); break;
            case "PropRelX": frames.Select(f => f.RelativeX).ShouldBe(new[] { 5f, 6f }); break;
            case "PropFrameLen": frames.Select(f => f.FrameLength).ShouldBe(new[] { 1.1f, 1.2f }); break;
            case "EventName": frames[0].Events[0].Name.ShouldBe("Footstep"); break;
            case "EventData": frames[0].Events[0].Data.ShouldBe("right"); break;
        }
    }

    [AvaloniaTheory]
    [InlineData("PropFrameLen")]
    [InlineData("PropRed")]
    [InlineData("PropChainName")]
    public async Task DeletingTheOpenFile_WhileAFieldIsTyped_DoesNotBringItBack(string field)
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);
        editor.Window.DeleteToRecycleBin = path => { File.Delete(path); return null; };
        editor.Services.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);

        if (field == "PropChainName")
        {
            editor.ClickRow(editor.ChainNamed("Walk"));
            editor.Control<TextBox>(field).Focus();
            editor.Type("X");
        }
        else Begin(editor, field, "7");
        await editor.Window.DeleteProjectFileAsync("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        File.Exists(hero).ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task ConvertingTheOpenFile_WhileALengthIsTyped_ConvertsTheTypedLength_AndDoesNotBringTheFileBack()
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);
        editor.Window.DeleteToRecycleBin = path => { File.Delete(path); return null; };
        editor.Dialogs.AnswerNextEditorDialog(_ => true);

        Begin(editor, "PropFrameLen", "0.5");
        _ = editor.Window.ConvertProjectFileToAchjAsync("hero.achx");
        editor.WaitUntil(() => editor.Dialogs.Shown.Count > 0 && !editor.Window.IsConverting, TimeSpan.FromSeconds(10)).ShouldBeTrue();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        File.Exists(hero).ShouldBeFalse();
        Saved(Path.ChangeExtension(hero, ".achj"))[0].FrameLength.ShouldBe(0.5f);
    }

    [AvaloniaTheory]
    [InlineData("PropFrameLen")]
    [InlineData("PropRed")]
    [InlineData("EventName")]
    [InlineData("tree rename")]
    public async Task DuplicatingTheOpenFile_WhileAFieldIsTyped_CopiesTheTypedValue(string field)
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(hero);

        if (field == "tree rename")
        {
            editor.ClickRow(editor.ChainNamed("Walk"));
            editor.Press(Key.F2);
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.Type("7");
        }
        else Begin(editor, field, "7");
        await editor.Window.DuplicateProjectFileAsync("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        foreach (string path in new[] { hero, Path.Combine(editor.ProjectFolder, "hero2.achx") })
        {
            var chain = AnimationEditorHarness.ReadSaved(path).AnimationChains[0];
            string? saved = field switch
            {
                "PropFrameLen" => chain.Frames[0].FrameLength.ToString(),
                "PropRed" => chain.Frames[0].Red.ToString(),
                "EventName" => chain.Frames[0].Events[0].Name,
                _ => chain.Name,
            };
            saved.ShouldBe("7", path);
        }
    }

    [AvaloniaFact]
    public async Task DuplicatingTheOpenSequenceFile_WhileASequenceIsRenamed_CopiesTheTypedName()
    {
        using var editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "One");
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(boss);
        editor.Click((Control)editor.Control<ListBox>("SequenceList").ContainerFromIndex(0)!);
        editor.Press(Key.F2);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.Type("Typed");

        await editor.Window.DuplicateProjectFileAsync("boss.seqj");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        SequenceListSave.FromFile(Path.Combine(editor.ProjectFolder, "boss2.seqj")).Sequences[0].Name.ShouldBe("Typed");
    }

    // The macOS menu bar takes no focus off the field.
    [AvaloniaTheory]
    [InlineData("Godot SpriteFrames…", "walk.tres")]
    [InlineData("PixiJS…", "walk.json")]
    [InlineData("Animated GIF…", "walk.gif")]
    public async Task Exporting_WhileAFieldIsTyped_ExportsTheTypedValue(string header, string name)
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        string target = Path.Combine(editor.ProjectFolder, name);
        editor.Dialogs.AnswerNextEditorDialog(_ => true);
        editor.Dialogs.AnswerNextSaveFile(target);

        bool pixi = name.EndsWith("json"); // exports no frame length
        if (pixi)
        {
            editor.ClickRow(editor.ChainNamed("Walk"));
            editor.Control<TextBox>("PropChainName").Focus();
            editor.Control<TextBox>("PropChainName").SelectAll();
            editor.Type("Typed");
        }
        else Begin(editor, "PropFrameLen", "0.5");
        NativeMenuTestSupport.Activate(NativeMenuTestSupport.Build(editor), "File", "Export", header);
        (await editor.WaitUntilAsync(() => editor.InfoText is not null || editor.ProblemText is not null, TimeSpan.FromSeconds(10))).ShouldBeTrue();

        if (pixi) File.ReadAllText(target).ShouldContain("Typed");
        else if (name.EndsWith("tres")) File.ReadAllText(target).ShouldContain("2.5"); // relative to the 0.2 s frame
        else
        {
            using var codec = SKCodec.Create(new SKMemoryStream(File.ReadAllBytes(target)));
            codec!.FrameInfo[0].Duration.ShouldBe(500);
        }
    }

    [AvaloniaFact]
    public async Task ClosingATab_WhoseTypedLengthCannotBeSaved_AsksFirst()
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        try
        {
            editor.Dialogs.AnswerNextSaveDiscardCancel(SaveDiscardCancelChoice.Cancel);

            Begin(editor, "PropFrameLen", "0.5");
            var tab = editor.CenterOf(editor.TabFor("hero.achx"));
            editor.Window.MouseDown(tab, MouseButton.Middle);
            editor.Window.MouseUp(tab, MouseButton.Middle);
            await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
            editor.ChainNamed("Walk").Frames[0].FrameLength.ShouldBe(0.5f);
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    // "* 0" makes both frames equal, so the box that was blank for "(mixed)" reads as a blank channel until it is refilled.
    [AvaloniaTheory]
    [InlineData("PropRed", "row")]
    [InlineData("PropRed", "tab click")]
    [InlineData("PropRed", "Enter")]
    [InlineData("PropAlpha", "row")]
    [InlineData("PropAlpha", "tab click")]
    [InlineData("PropAlpha", "Enter")]
    public async Task ARelativeColorEditThatMakesMixedFramesEqual_IsOneStep_AndClearsNothing(string field, string way)
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        string other = WriteFixture(editor, "other.achx");
        await editor.OpenAsync(other);
        await editor.OpenAsync(hero);
        var frames = editor.ChainNamed("Walk").Frames;
        (frames[0].Alpha, frames[1].Alpha) = (10, 20);

        Begin(editor, field, "* 0", both: true);
        if (way == "row") editor.ClickRow(editor.ChainNamed("Walk"));
        else if (way == "Enter") editor.Press(Key.Enter);
        else editor.ClickTab("other.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        if (way == "tab click") editor.ClickTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        frames.Select(f => field == "PropRed" ? f.Red : f.Alpha).ShouldBe(new int?[] { 0, 0 });
        editor.UndoLabels.Count.ShouldBe(1);
    }

    [AvaloniaTheory]
    [InlineData("PropFrameLen", "Ctrl+Z")]
    [InlineData("PropFrameLen", "Ctrl+Y")]
    [InlineData("PropFrameLen", "F3")]
    [InlineData("PropRed", "Ctrl+Z")]
    [InlineData("PropRed", "Ctrl+Y")]
    [InlineData("PropRed", "F3")]
    [InlineData("EventName", "Ctrl+Z")]
    [InlineData("EventName", "Ctrl+Y")]
    public async Task AHotkeyThatDoesNotLeaveTheDocument_CommitsNoTypedField(string field, string hotkey)
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);
        Begin(editor, "PropRelX", "9");
        editor.Press(Key.Enter);
        if (hotkey == "Ctrl+Y") editor.Press(Key.Z, RawInputModifiers.Control);

        Begin(editor, field, "0.5");
        if (hotkey == "F3") editor.Press(Key.F3);
        else editor.Press(hotkey == "Ctrl+Z" ? Key.Z : Key.Y, RawInputModifiers.Control);

        var frame = editor.ChainNamed("Walk").Frames[0];
        (frame.FrameLength, frame.Red, frame.Events[0].Name).ShouldBe((0.1f, 10, "Step"));
        frame.RelativeX.ShouldBe(hotkey == "Ctrl+Z" ? 1f : 9f);
        editor.UndoManager.CanRedo.ShouldBe(hotkey == "Ctrl+Z");
    }
}
