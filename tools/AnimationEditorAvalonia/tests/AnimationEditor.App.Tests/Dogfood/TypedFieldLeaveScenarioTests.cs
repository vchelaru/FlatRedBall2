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
    public async Task CtrlZ_WhileTypingALength_TakesTheTypedLengthBack()
    {
        using var editor = new AnimationEditorHarness();
        string hero = WriteFixture(editor);
        await editor.OpenAsync(hero);

        Begin(editor, "PropFrameLen", "0.5");
        editor.Press(Key.Z, RawInputModifiers.Control);

        Saved(hero)[0].FrameLength.ShouldBe(0.1f);
        editor.UndoLabels.ShouldBeEmpty();
    }
}
