using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Issue #1216: hovering a chain or frame row in the tree highlights its region(s) on the
/// wireframe in a hover style, separate from the selection highlight.
/// </summary>
public class TreeHoverScenarioTests
{
    private const int Sheet = 128;

    private static async Task<AnimationEditorHarness> OpenTwoChainsAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", Sheet, Sheet);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 32, 32), (64, 0, 32, 32)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 64, 32, 32), (64, 64, 32, 32)));
        await editor.OpenAsync(path);
        return editor;
    }

    private static List<(float, float, float, float)> HoverRects(AnimationEditorHarness editor) =>
        editor.Wireframe.GetTreeHoverFrameBounds()
            .Select(r => (r.Left, r.Top, r.Width, r.Height)).ToList();

    [AvaloniaFact]
    public async Task HoveringAFrameRow_HighlightsJustThatFrame()
    {
        using AnimationEditorHarness editor = await OpenTwoChainsAsync();
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.Hover(editor.RowHeaderPoint(walk.Frames[1]));

        HoverRects(editor).ShouldBe(new[] { (64f, 0f, 32f, 32f) });
    }

    [AvaloniaFact]
    public async Task HoveringAChainRow_HighlightsEveryFrameOfThatChain()
    {
        using AnimationEditorHarness editor = await OpenTwoChainsAsync();
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.Hover(editor.RowHeaderPoint(run));

        HoverRects(editor).ShouldBe(new[] { (0f, 64f, 32f, 32f), (64f, 64f, 32f, 32f) });
    }

    [AvaloniaFact]
    public async Task HoveringTheSelectedFrame_AddsNoHoverHighlight()
    {
        using AnimationEditorHarness editor = await OpenTwoChainsAsync();
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);

        editor.Hover(editor.RowHeaderPoint(walk.Frames[0]));

        HoverRects(editor).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task MovingThePointerOffTheTree_ClearsTheHoverHighlight()
    {
        using AnimationEditorHarness editor = await OpenTwoChainsAsync();
        AnimationChainSave walk = editor.ChainNamed("Walk");
        AnimationChainSave run = editor.ChainNamed("Run");
        editor.ClickRow(walk);
        editor.Hover(editor.RowHeaderPoint(run));
        HoverRects(editor).ShouldNotBeEmpty();

        editor.Hover(editor.WireframeRectOf(walk.Frames[0]).Center);

        HoverRects(editor).ShouldBeEmpty();
    }
}
