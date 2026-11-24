using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The pop-ups float over the panes and never take a click meant for the title bar, the menu or the tab strip.</summary>
public class NotificationPlacementScenarioTests
{
    private const string LongMessage =
        "Auto save failed — Access to the path 'C:/projects/game/content/animations/boss.seqj' is denied. Your edits are kept in memory.";

    /// <summary>Taller than the window at any pop-up width.</summary>
    private static readonly string VeryLongMessage = string.Concat(Enumerable.Repeat(LongMessage + " ", 60));

    [AvaloniaTheory]
    [InlineData(900, 3)]
    [InlineData(1280, 3)]
    [InlineData(1280, 5)]
    [InlineData(1920, 8)]
    public async Task PopUps_AtAnyWidthAndTabCount_LeaveTabCloseButtonsClickable(int width, int tabCount)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Window.Width = width;
        List<string> names = Enumerable.Range(1, tabCount).Select(i => $"sequences{i}.seqj").ToList();
        foreach (string name in names)
        {
            await editor.OpenAsync(editor.WriteSeqj(name, "Attack"));
        }

        ShowBoth(editor, VeryLongMessage);

        foreach (string name in names)
        {
            Button close = editor.TabFor(name).GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "✕");
            ShouldBeHittable(editor, close, $"{name}'s close button");
        }
    }

    [AvaloniaTheory]
    [InlineData(900)]
    [InlineData(1280)]
    public void PopUps_LeaveTheWindowCloseButtonClickable(int width)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Window.Width = width;

        ShowBoth(editor, VeryLongMessage);

        ShouldBeHittable(editor, editor.Control<Button>("CloseBtn"), "the window's close button");
    }

    [AvaloniaTheory]
    [InlineData(900)]
    [InlineData(1280)]
    public void PopUps_LeaveTheMenuClickable(int width)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Window.Width = width;

        ShowBoth(editor, VeryLongMessage);

        foreach (MenuItem item in editor.Control<Menu>("MainMenu").Items.OfType<MenuItem>())
        {
            ShouldBeHittable(editor, item, $"the {item.Header} menu");
        }
    }

    [AvaloniaTheory]
    [InlineData(900)]
    [InlineData(1280)]
    public async Task PopUps_OverEveryPane_AreOnTopAndLeaveTheToolbarAndEachOtherUncovered(int width)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Window.Width = width;
        string png = editor.WritePng("sheet.png", 64, 64);
        string achx = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string seqj = editor.WriteSeqj("boss.seqj", "Attack");

        await editor.OpenAsync(achx);
        ShouldBothBeUsable("MagicWandToggle");
        await editor.OpenPngTabAsync(png);
        ShouldBothBeUsable("PngZoom");
        await editor.OpenAsync(seqj);
        ShouldBothBeUsable("SequencePlayButton");

        void ShouldBothBeUsable(string toolbarControl)
        {
            ShowBoth(editor, LongMessage);
            ShouldBeHittable(editor, editor.Notifications.ErrorBannerRetryBtn, "the banner's Retry");
            ShouldBeHittable(editor, editor.Notifications.ErrorBannerDismissBtn, "the banner's dismiss button");
            ShouldBeHittable(editor, editor.Notifications.ToastDismissBtn, "the toast's dismiss button");
            ShouldBeHittable(editor, editor.Control<Control>(toolbarControl), toolbarControl);
        }
    }

    [AvaloniaTheory]
    [InlineData(900)]
    [InlineData(1280)]
    public void PopUps_SpanTheWholeWindowWidth_EvenWithASidebarWiderThanTheWindow(int width)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Window.Width = width;
        foreach (double sidebar in new double[] { 300, 1400 })
        {
            editor.Window.MainContentGrid.ColumnDefinitions[0].Width = new GridLength(sidebar, GridUnitType.Pixel);

            ShowBoth(editor, LongMessage);

            RectIn(editor, editor.Notifications.ToastPanel).Right.ShouldBe(width - 16, tolerance: 1);
            RectIn(editor, editor.Notifications.ErrorBanner).Center.X.ShouldBe(width / 2.0, tolerance: 1);
        }
    }

    [AvaloniaFact]
    public void VeryLongMessages_StayInsideTheSmallestWindow_WithTheirButtonsReachable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Window.Width = 900;
        editor.Window.Height = 600;

        ShowBoth(editor, VeryLongMessage);

        Rect content = RectIn(editor, editor.Window.MainContentGrid);
        content.Contains(RectIn(editor, editor.Notifications)).ShouldBeTrue("the overlay reaches past the content area");
        content.Contains(RectIn(editor, editor.Notifications.ErrorBanner)).ShouldBeTrue("the banner leaves the content area");
        content.Contains(RectIn(editor, editor.Notifications.ToastPanel)).ShouldBeTrue("the toast leaves the content area");
        RectIn(editor, editor.Notifications.ErrorBanner).Intersects(RectIn(editor, editor.Notifications.ToastPanel)).ShouldBeFalse();
        ShouldBeHittable(editor, editor.Notifications.ErrorBannerRetryBtn, "the banner's Retry");
        ShouldBeHittable(editor, editor.Notifications.ErrorBannerCopyBtn, "the banner's copy button");
        ShouldBeHittable(editor, editor.Notifications.ErrorBannerDismissBtn, "the banner's dismiss button");
        ShouldBeHittable(editor, editor.Notifications.ToastCopyBtn, "the toast's copy button");
        ShouldBeHittable(editor, editor.Notifications.ToastDismissBtn, "the toast's dismiss button");
    }

    [AvaloniaFact]
    public async Task EmptySpaceAroundThePopUps_PassesClicksToThePane()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));

        ShowBoth(editor, LongMessage);

        ShouldBeHittable(editor, editor.Control<Button>("AddChainBtn"), "Add Animation");
        ShouldBeHittable(editor, editor.Wireframe, "the wireframe");
    }

    /// <summary>A toast that moved when the banner's timer ran out would slide away from a click aimed at it.</summary>
    [AvaloniaFact]
    public void Toast_StaysPut_WhenTheBannerHides()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        ShowBoth(editor, LongMessage);
        Rect before = RectIn(editor, editor.Notifications.ToastPanel);

        editor.Notifications.HideProblem("Auto save failed");
        editor.Layout();

        editor.ProblemText.ShouldBeNull();
        RectIn(editor, editor.Notifications.ToastPanel).ShouldBe(before);
    }

    private static void ShowBoth(AnimationEditorHarness editor, string message)
    {
        editor.Window.ShowProblem(message, retry: () => { });
        editor.Window.ShowInfo(message);
        editor.Layout();
    }

    private static Rect RectIn(AnimationEditorHarness editor, Control control) =>
        new Rect(control.TranslatePoint(new Point(0, 0), editor.Window)!.Value, control.Bounds.Size);

    private static void ShouldBeHittable(AnimationEditorHarness editor, Control control, string what)
    {
        Visual? hit = editor.Window.InputHitTest(editor.CenterOf(control)) as Visual;

        (hit != null && (hit == control || control.IsVisualAncestorOf(hit)))
            .ShouldBeTrue($"a click on {what} lands on {hit?.GetType().Name} '{(hit as Control)?.Name}'");
    }
}
