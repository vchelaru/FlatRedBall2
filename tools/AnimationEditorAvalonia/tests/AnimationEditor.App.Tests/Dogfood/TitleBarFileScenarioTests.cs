using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The title bar's file name, its menu and the status bar's file name are about the document in front.</summary>
public class TitleBarFileScenarioTests
{
    // An Untitled document has no file, so the title is the app name alone, as after File > New.
    [AvaloniaTheory]
    [InlineData("Add Animation")]
    [InlineData("paste")]
    public async Task NewDocumentAfterClosingTheLastTab_NamesNoFile(string way)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.C, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        ShouldName(editor, "hero.achx");

        editor.CloseTab("hero.achx");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));
        ShouldName(editor, "");

        editor.AnimTree.Focus();
        if (way == "paste")
            editor.Press(Key.V, RawInputModifiers.Control);
        else
        {
            editor.Click(editor.Control<Button>("AddChainBtn"));
            editor.Wait(TimeSpan.FromMilliseconds(50));
            editor.Press(Key.Enter);
        }
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.TabLabels.ShouldBe(["Untitled"]);
        ShouldName(editor, "");
        editor.Window.CloseWithoutPrompt();
    }

    [AvaloniaTheory]
    [InlineData("boss.seqj")]
    [InlineData("sheet.png")]
    [InlineData("hero.achx")]
    public async Task CopyFullPath_CopiesTheFileInFront(string inFront)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteSeqj("boss.seqj", "Attack");
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        string path = Path.Combine(editor.ProjectFolder, inFront);
        if (path != hero) await editor.OpenAsync(path);
        ShouldName(editor, inFront);

        editor.Control<TextBlock>("TitleFileName").ContextMenu!.Items.OfType<MenuItem>()
            .Single(item => Equals(item.Header, "Copy Full Path")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        string? copied = await editor.Window.Clipboard!.TryGetTextAsync();
        Path.GetFileName(copied).ShouldBe(inFront);
    }

    private static void ShouldName(AnimationEditorHarness editor, string file)
    {
        editor.Window.Title.ShouldBe(file == "" ? "AnimationEditor" : $"AnimationEditor - {file}");
        editor.TitleFileName.ShouldBe(file);
        editor.Control<TextBlock>("StatusFilename").Text.ShouldBe(file);
    }
}
