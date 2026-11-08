using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

// A TextBox refuses the editor's own copy JSON unless it opts in with EditorTextPaste.AllowEditorJson.
public class EditorTextPasteTests
{
    private static string CopiedJson() => SequenceClipboard.Write(new SequenceSave { Name = "Idle" }, @"C:\game");

    private static async Task<string?> PasteAsync(Window window, TextBox box, string clipboard, bool withCtrlV)
    {
        await window.Clipboard!.SetTextAsync(clipboard);
        box.Focus();
        box.SelectAll();
        if (withCtrlV)
        {
            window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
            window.KeyRelease(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
        }
        else box.Paste();
        await Task.Delay(100);
        Dispatcher.UIThread.RunJobs();
        return box.Text;
    }

    private static (Window Window, TextBox Box) Show()
    {
        var box = new TextBox { Text = "Walk" };
        var window = new Window { Content = box };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, box);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APlainTextBox_RefusesTheEditorsCopyJson(bool withCtrlV)
    {
        var (window, box) = Show();

        Assert.Equal("Walk", await PasteAsync(window, box, CopiedJson(), withCtrlV));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task APlainTextBox_TakesOrdinaryText(bool withCtrlV)
    {
        var (window, box) = Show();

        Assert.Equal("hello", await PasteAsync(window, box, "hello", withCtrlV));
    }

    [AvaloniaFact]
    public async Task ATextBoxThatAllowsEditorJson_TakesIt()
    {
        var (window, box) = Show();
        EditorTextPaste.SetAllowEditorJson(box, true);
        string json = CopiedJson();

        Assert.Equal("{", await PasteAsync(window, box, json, withCtrlV: false)); // a one-line box keeps the first line
    }

    [AvaloniaFact]
    public async Task APasteAfterARefusedOne_StillWorks()
    {
        var (window, box) = Show();
        await PasteAsync(window, box, CopiedJson(), withCtrlV: false);

        Assert.Equal("hello", await PasteAsync(window, box, "hello", withCtrlV: false));
        Assert.Equal("hello", await PasteAsync(window, box, CopiedJson(), withCtrlV: false));
    }

    [AvaloniaFact]
    public async Task TheSearchBox_TakesAnyText()
    {
        var search = new SearchToggleBox();
        var window = new Window { Content = search };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        string json = CopiedJson();

        Assert.Equal("{", await PasteAsync(window, search.SearchBox, json, withCtrlV: false));
    }

    [AvaloniaFact]
    public async Task AFrameEventRow_RefusesTheJsonInItsName_ButTakesItInItsData()
    {
        var editor = new FrameEventsEditor();
        var window = new Window { Content = editor };
        window.Show();
        editor.ShowEvents(new[] { new AnimationFrameEvent { Name = "Step", Data = "left" } });
        Dispatcher.UIThread.RunJobs();
        string json = CopiedJson();

        Assert.Equal("Step", await PasteAsync(window, editor.Rows[0].Name, json, withCtrlV: false));
        Assert.Equal("{", await PasteAsync(window, editor.Rows[0].Data, json, withCtrlV: false));
    }
}
