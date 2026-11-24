using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A strip with more tabs than fit shows a horizontal scrollbar, which must not take the clicks meant for
/// the tabs, before or after its expand delay (#2034).
/// </summary>
public class TabStripOverflowScenarioTests
{
    private const int PastExpandDelayMs = 800;

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(PastExpandDelayMs)]
    public async Task HoverThenClickCloseButton_OnAnOverflowingStrip_ClosesTheTab(int hoverMs)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        List<string> names = await OpenTabsAsync(editor, 8);
        ScrollBarOf(editor).IsVisible.ShouldBeTrue("the strip should overflow");
        Button close = editor.TabFor(names[2]).GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "✕");

        await HoverThenClickAsync(editor, editor.PointIn(close, close.Bounds.Width / 2, close.Bounds.Height - 1), hoverMs);
        await editor.WaitUntilAsync(() => !editor.TabLabels.Contains(names[2]), TimeSpan.FromSeconds(2));

        editor.TabLabels.ShouldNotContain(names[2]);
        editor.TabLabels.Count.ShouldBe(7);
    }

    [AvaloniaTheory]
    [InlineData(0)]
    [InlineData(PastExpandDelayMs)]
    public async Task HoverThenClickTheBottomOfATab_OnAnOverflowingStrip_SwitchesToIt(int hoverMs)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        List<string> names = await OpenTabsAsync(editor, 8);
        Border tab = editor.TabFor(names[2]);

        await HoverThenClickAsync(editor, editor.PointIn(tab, 20, tab.Bounds.Height - 1), hoverMs);
        await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab!.DisplayName == names[2], TimeSpan.FromSeconds(2));

        editor.Tabs.ActiveTab!.DisplayName.ShouldBe(names[2]);
    }

    [AvaloniaFact]
    public async Task Strip_GrowsByTheScrollBarOnlyWhenItOverflows()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenTabsAsync(editor, 1);
        Border bar = editor.Control<Border>("TabBarBorder");
        bar.Bounds.Height.ShouldBe(30);

        await OpenTabsAsync(editor, 8);

        bar.Bounds.Height.ShouldBe(30 + ScrollBarOf(editor).Bounds.Height);
        editor.TabFor("long sequence file 1.seqj").Bounds.Height.ShouldBe(29);
    }

    private static async Task<List<string>> OpenTabsAsync(AnimationEditorHarness editor, int count)
    {
        List<string> names = Enumerable.Range(1, count).Select(i => $"long sequence file {i}.seqj").ToList();
        foreach (string name in names)
        {
            await editor.OpenAsync(editor.WriteSeqj(name, "Attack"));
        }
        editor.Layout();
        return names;
    }

    private static ScrollBar ScrollBarOf(AnimationEditorHarness editor) =>
        editor.Control<StackPanel>("TabStrip").FindAncestorOfType<ScrollViewer>()!.GetVisualDescendants()
            .OfType<ScrollBar>().Single(bar => bar.Orientation == Orientation.Horizontal);

    private static async Task HoverThenClickAsync(AnimationEditorHarness editor, Point target, int hoverMs)
    {
        Point start = target - new Point(40, 4);
        for (int step = 0; step <= 10; step++)
        {
            editor.Window.MouseMove(start + (target - start) * (step / 10.0));
            editor.Layout();
        }
        await editor.WaitAsync(TimeSpan.FromMilliseconds(hoverMs));
        editor.Window.MouseDown(target, MouseButton.Left);
        editor.Window.MouseUp(target, MouseButton.Left);
        editor.Layout();
    }
}
