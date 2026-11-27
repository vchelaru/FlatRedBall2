using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Right-clicking the texture on either canvas offers "Open &lt;file&gt; in New Tab", which opens that PNG in its own
/// tab (#2197).
/// </summary>
public class TextureMenuOpenInTabScenarioTests
{
    private static async Task<AnimationEditorHarness> OpenWithFrameSelectedAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        var walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        return editor;
    }

    private static async Task PickOpenInNewTabAsync(AnimationEditorHarness editor, Control canvas)
    {
        editor.RightClick(canvas);
        ContextMenu menu = canvas.ContextMenu!;
        menu.IsOpen.ShouldBeTrue();
        editor.PickMenuItem(menu, "Open sheet.png in New Tab");
        (await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab?.DisplayName == "sheet.png", TimeSpan.FromSeconds(5)))
            .ShouldBeTrue($"the active tab is {editor.Tabs.ActiveTab?.DisplayName}");
        await editor.Window.WhenPngTabLoaded();
        editor.Layout();
    }

    [AvaloniaFact]
    public async Task WireframeMenu_OpenInNewTab_OpensThePngTab()
    {
        using var editor = await OpenWithFrameSelectedAsync();

        await PickOpenInNewTabAsync(editor, editor.Wireframe);

        editor.TabLabels.ShouldBe(new[] { "hero.achx", "sheet.png" });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task PreviewMenu_OpenInNewTab_OpensThePngTab()
    {
        using var editor = await OpenWithFrameSelectedAsync();

        await PickOpenInNewTabAsync(editor, editor.Preview);

        editor.TabLabels.ShouldBe(new[] { "hero.achx", "sheet.png" });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task WireframeMenu_OpenInNewTab_PngAlreadyOpen_FocusesItWithoutASecondTab()
    {
        using var editor = await OpenWithFrameSelectedAsync();
        await editor.OpenPngTabAsync(Path.Combine(editor.ProjectFolder, "sheet.png"));
        editor.ClickTab("hero.achx");
        editor.ClickRow(editor.ChainNamed("Walk").Frames[0]);

        await PickOpenInNewTabAsync(editor, editor.Wireframe);

        editor.TabLabels.ShouldBe(new[] { "hero.achx", "sheet.png" });
    }
}
