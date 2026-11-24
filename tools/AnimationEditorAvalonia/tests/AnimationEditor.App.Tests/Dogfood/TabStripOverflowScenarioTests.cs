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
/// the tabs, before or after its expand delay (#2034). A tab that becomes active scrolls into view (#2040).
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
        ScrollToStart(editor);
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
        ScrollToStart(editor);
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

    [AvaloniaFact]
    public async Task ActiveTab_OpenedOrSwitchedToPastEitherEdge_ScrollsIntoView()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        List<string> names = await OpenTabsAsync(editor, 12);
        ShouldShowActiveTab(editor, names[11]);

        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, names[0]));
        ShouldShowActiveTab(editor, names[0]);

        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, names[11]));
        ShouldShowActiveTab(editor, names[11]);

        editor.Press(Key.Left, RawInputModifiers.Alt);
        await editor.WaitUntilAsync(() => editor.Tabs.ActiveTab!.DisplayName == names[0], TimeSpan.FromSeconds(2));
        ShouldShowActiveTab(editor, names[0]);
    }

    [AvaloniaFact]
    public async Task ActiveTab_Closed_ScrollsTheTabThatTakesOverIntoView()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        List<string> names = await OpenTabsAsync(editor, 12);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, names[0]));
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, names[11]));

        editor.CloseTab(names[11]);
        await editor.WaitUntilAsync(() => editor.TabLabels.Count == 11, TimeSpan.FromSeconds(2));

        ShouldShowActiveTab(editor, names[0]);
    }

    [AvaloniaFact]
    public async Task ActiveTab_RestoredPastTheEdge_ScrollsIntoView()
    {
        using AnimationEditorHarness first = new AnimationEditorHarness();
        List<string> names = await OpenTabsAsync(first, 12);

        using AnimationEditorHarness restarted = new AnimationEditorHarness(first.Services.SettingsRoot);
        await restarted.WaitUntilAsync(() => restarted.TabLabels.Count == 12, TimeSpan.FromSeconds(5));

        ShouldShowActiveTab(restarted, names[11]);
    }

    [AvaloniaFact]
    public async Task ScrolledAwayFromTheActiveTab_BackgroundCloseOrTabDrag_LeavesTheScrollOffset()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        List<string> names = await OpenTabsAsync(editor, 12);
        ScrollToStart(editor);

        editor.CloseTab(names[1]);
        await editor.WaitUntilAsync(() => editor.TabLabels.Count == 11, TimeSpan.FromSeconds(2));
        ScrollViewerOf(editor).Offset.X.ShouldBe(0);

        Border target = editor.TabFor(names[3]);
        editor.Drag(editor.PointIn(editor.TabFor(names[0]), 20, 10), editor.PointIn(target, target.Bounds.Width - 30, 10));
        editor.TabLabels[0].ShouldBe(names[2], "the drag should have moved the first tab");
        ScrollViewerOf(editor).Offset.X.ShouldBe(0);
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe(names[11]);
    }

    [AvaloniaFact]
    public async Task ClickATabAlreadyInFullView_LeavesTheScrollOffset()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        List<string> names = await OpenTabsAsync(editor, 12);
        ScrollViewerOf(editor).Offset = new Vector(50, 0);
        editor.Layout();

        editor.ClickTab(names[2]);

        ShouldShowActiveTab(editor, names[2]);
        ScrollViewerOf(editor).Offset.X.ShouldBe(50);
    }

    private static void ScrollToStart(AnimationEditorHarness editor)
    {
        ScrollViewerOf(editor).Offset = default;
        editor.Layout();
        // Hit-testing is a render tick behind the new offset, see AnimationEditorHarness.ScrollIntoView.
        editor.Layout();
    }

    private static void ShouldShowActiveTab(AnimationEditorHarness editor, string name)
    {
        editor.Layout();
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe(name);
        ScrollViewer viewer = ScrollViewerOf(editor);
        Border tab = editor.TabFor(name);
        double left = tab.TranslatePoint(default, viewer)!.Value.X;
        left.ShouldBeGreaterThanOrEqualTo(0, $"{name} is cut off on the left");
        (left + tab.Bounds.Width).ShouldBeLessThanOrEqualTo(viewer.Viewport.Width, $"{name} is cut off on the right");
    }

    private static ScrollViewer ScrollViewerOf(AnimationEditorHarness editor) =>
        editor.Control<StackPanel>("TabStrip").FindAncestorOfType<ScrollViewer>()!;

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
        ScrollViewerOf(editor).GetVisualDescendants()
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
