using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// On a Mac, Cmd+C/X/V are key equivalents of the system menu's Edit items, so AppKit runs the menu action and the
/// focused text box never sees the key. The Edit items therefore have to act on the text box's own text whenever one has
/// focus, and on the selected chain, frame or sequence otherwise.
/// </summary>
public class EditMenuTextBoxScenarioTests
{
    [AvaloniaFact]
    public async Task EditCopyCutAndPaste_WithATextBoxFocused_ActOnItsText_AndLeaveTheChainsAlone()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Run"));
        NativeMenu bar = NativeMenuTestSupport.Build(editor);
        void Choose(string item) => NativeMenuTestSupport.Find(bar, "Edit", item).Command!.Execute(null);
        TextBox box = editor.Control<TextBox>("PropChainName");
        IClipboard clipboard = editor.Window.Clipboard!;
        await clipboard.SetTextAsync("");
        box.Focus();
        editor.Layout();
        box.SelectAll();

        Choose("Copy");
        (await clipboard.TryGetTextAsync()).ShouldBe("Run");
        box.Text.ShouldBe("Run");

        Choose("Cut");
        box.Text.ShouldBeNullOrEmpty();
        (await clipboard.TryGetTextAsync()).ShouldBe("Run");

        await clipboard.SetTextAsync("Sprint");
        Choose("Paste");
        box.Text.ShouldBe("Sprint");

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
    }
}
