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

    [AvaloniaTheory]
    [InlineData(900, 3)]
    [InlineData(1280, 3)]
    [InlineData(1280, 5)]
    [InlineData(1920, 8)]
    public async Task PopUps_AtAnyWidthAndTabCount_LeaveTabsMenuAndWindowButtonsClickable(int width, int tabCount)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.Window.Width = width;
        List<string> names = Enumerable.Range(1, tabCount).Select(i => $"sequences{i}.seqj").ToList();
        foreach (string name in names)
        {
            await editor.OpenAsync(editor.WriteSeqj(name, "Attack"));
        }
        editor.Window.ShowProblem(LongMessage, retry: () => { });
        editor.Window.ShowInfo(LongMessage);

        foreach (string name in names)
        {
            Button close = editor.TabFor(name).GetVisualDescendants().OfType<Button>().First(button => button.Content as string == "✕");
            editor.ScrollIntoView(close);
            ShouldBeHittable(editor, close, $"{name}'s close button");
        }
        foreach (MenuItem item in editor.Control<Menu>("MainMenu").Items.OfType<MenuItem>())
        {
            ShouldBeHittable(editor, item, $"the {item.Header} menu");
        }
        ShouldBeHittable(editor, editor.Control<Button>("CloseBtn"), "the window's close button");
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
            editor.Window.ShowProblem(LongMessage, retry: () => { });
            editor.Window.ShowInfo(LongMessage);
            editor.Layout();
            ShouldBeHittable(editor, editor.Notifications.ErrorBannerRetryBtn, "the banner's Retry");
            ShouldBeHittable(editor, editor.Notifications.ErrorBannerDismissBtn, "the banner's dismiss button");
            ShouldBeHittable(editor, editor.Notifications.ToastDismissBtn, "the toast's dismiss button");
            ShouldBeHittable(editor, editor.Control<Control>(toolbarControl), toolbarControl);
        }
    }

    private static void ShouldBeHittable(AnimationEditorHarness editor, Control control, string what)
    {
        Visual? hit = editor.Window.InputHitTest(editor.CenterOf(control)) as Visual;

        (hit != null && (hit == control || control.IsVisualAncestorOf(hit)))
            .ShouldBeTrue($"a click on {what} lands on {hit?.GetType().Name} '{(hit as Control)?.Name}'");
    }
}
