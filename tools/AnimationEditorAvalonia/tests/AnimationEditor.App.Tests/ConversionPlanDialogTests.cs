using AnimationEditor.Core.Conversions;
using AnimationEditor.Views.Dialogs;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>The Convert dialog: the keyboard, the one scroll area, and the counts that stay in view (#2135).</summary>
public class ConversionPlanDialogTests
{
    private static AchxConversionPlan Plan(int files, int skipped = 0) => new("/game",
        Enumerable.Range(1, files).Select(i => new PlannedFile($"/game/Character{i:000}.achx", $"/game/Character{i:000}.achj", null, 2, 10))
            .Concat(Enumerable.Range(1, skipped).Select(i => new PlannedFile($"/game/Bad{i}.achx", $"/game/Bad{i}.achj", new ConversionProblem("Can't be read", "Line 1.")))).ToList(),
        [new PlannedSequence("/game/attack.seqj", ["/game/Character001.achx"], 2)]);

    private static (Window Owner, Window Dialog, Task<string?> Answer) Show(
        AchxConversionPlan plan, double ownerHeight = 700, Func<IProgress<ConversionProgress>, Task<string>>? convert = null)
    {
        var owner = new Window { Width = 900, Height = ownerHeight };
        owner.Show();
        var answer = EditorDialogs.ShowConversionPlanAsync(
            new WindowEditorDialogHost(owner), plan, isNewPlan: false, convert ?? (_ => Task.FromResult("done")));
        var dialog = owner.OwnedWindows.Single();
        Settle(dialog);
        return (owner, dialog, answer);
    }

    private static void Settle(Window dialog)
    {
        for (int i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            dialog.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void Press(Window dialog, Key key)
    {
        dialog.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        Settle(dialog);
    }

    private static Button Row(Window dialog, string name) =>
        dialog.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetName(button)?.StartsWith(name) == true);

    private static ScrollViewer Scroller(Window dialog) => dialog.GetVisualDescendants().OfType<ScrollViewer>().Single(viewer => viewer.Content is StackPanel);

    private static TextBlock Text(Window dialog, string text) => dialog.GetVisualDescendants().OfType<TextBlock>().First(block => block.Text == text);

    private static bool Shows(Window dialog, string text) =>
        dialog.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == text && block.IsEffectivelyVisible);

    [AvaloniaTheory]
    [InlineData(Key.Enter, true)]
    [InlineData(Key.Escape, false)]
    public async Task AsOpened_EnterConverts_EscapeCancels(Key key, bool converts)
    {
        var (owner, dialog, answer) = Show(Plan(3));

        Press(dialog, key);

        (await answer).ShouldBe(converts ? "done" : null);
        owner.Close();
    }

    [AvaloniaFact]
    public async Task Enter_OnARow_OpensIt_AndConvertsNothing()
    {
        var (owner, dialog, answer) = Show(Plan(3, skipped: 1));
        Shows(dialog, "Line 1.").ShouldBeFalse();
        for (int i = 0; i < 2; i++) Press(dialog, Key.Tab); // Convert, Cancel, the first row
        Row(dialog, "Bad1.achx").IsFocused.ShouldBeTrue();

        Press(dialog, Key.Enter);

        Shows(dialog, "Line 1.").ShouldBeTrue();
        answer.IsCompleted.ShouldBeFalse();
        Press(dialog, Key.Escape);
        (await answer).ShouldBeNull();
        owner.Close();
    }

    [AvaloniaFact]
    public async Task NothingToConvert_EnterOnlyCloses()
    {
        var (owner, dialog, answer) = Show(Plan(0, skipped: 2));

        Press(dialog, Key.Enter);

        (await answer).ShouldBeNull();
        owner.Close();
    }

    [AvaloniaFact]
    public void ManyFiles_OneScrollArea_TheCountStaysInView_AndOnlyVisibleRowsExist()
    {
        var (owner, dialog, _) = Show(Plan(200, skipped: 1));
        Shows(dialog, "Character001.achx → .achj").ShouldBeFalse("a long group starts closed");

        Row(dialog, "200 become").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(dialog);
        var scroller = Scroller(dialog);
        double top = scroller.TranslatePoint(default, dialog)!.Value.Y;
        double buttons = Text(dialog, "Cancel").TranslatePoint(default, dialog)!.Value.Y;
        scroller.Offset = new Vector(0, 2000);
        Settle(dialog);

        dialog.Height.ShouldBeLessThanOrEqualTo(owner.ClientSize.Height);
        scroller.Extent.Height.ShouldBeGreaterThan(200 * 24);
        double count = Text(dialog, "Animations").TranslatePoint(default, dialog)!.Value.Y;
        count.ShouldBeInRange(top, top + 40);
        Text(dialog, "Cancel").TranslatePoint(default, dialog)!.Value.Y.ShouldBe(buttons);
        dialog.GetVisualDescendants().OfType<TextBlock>().Count(block => block.Text?.EndsWith(" → .achj") == true).ShouldBeLessThan(60);
        dialog.GetVisualDescendants().OfType<ScrollViewer>().Where(viewer => viewer.Extent.Height > viewer.Viewport.Height).ShouldBe([scroller]);
        owner.Close();
    }

    [AvaloniaFact]
    public void ASmallWindow_IsNotOutgrown()
    {
        var (owner, dialog, _) = Show(Plan(6, skipped: 4), ownerHeight: 400);

        dialog.ClientSize.Height.ShouldBeLessThanOrEqualTo(400);
        Text(dialog, "Cancel").TranslatePoint(default, dialog)!.Value.Y.ShouldBeInRange(0, 400);
        owner.Close();
    }

    [AvaloniaFact]
    public async Task WhileItConverts_TheDialogStays_AndSaysWhatItIsDoing()
    {
        var work = new TaskCompletionSource<string>();
        IProgress<ConversionProgress>? progress = null;
        int runs = 0;
        var (owner, dialog, answer) = Show(Plan(3), convert: report => { runs++; progress = report; return work.Task; });

        Press(dialog, Key.Enter);
        ProgressBar bar = dialog.GetVisualDescendants().OfType<ProgressBar>().Single();
        bar.Value.ShouldBe(0, "the files are checked, which is not counted");
        progress!.Report(new ConversionProgress("Converting", 2, 3));
        Settle(dialog);
        (bar.Value / bar.Maximum).ShouldBe(2 / 3d);
        bar.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_Indicator").Bounds.Width.ShouldBe(bar.Bounds.Width * 2 / 3, 1);
        Shows(dialog, "Converting 2 of 3").ShouldBeTrue();
        Press(dialog, Key.Escape);
        Press(dialog, Key.Enter);
        dialog.Close();
        Settle(dialog);
        (answer.IsCompleted, dialog.IsVisible, runs).ShouldBe((false, true, 1));

        work.SetResult("done");
        Settle(dialog);
        (await answer).ShouldBe("done");
        dialog.IsVisible.ShouldBeFalse();
        owner.Close();
    }

    [AvaloniaFact]
    public async Task WhenItThrows_TheDialogCloses_AndTheErrorIsTheCallers()
    {
        var (owner, dialog, answer) = Show(Plan(3), convert: _ => throw new InvalidOperationException("no"));

        Press(dialog, Key.Enter);

        dialog.IsVisible.ShouldBeFalse();
        (await Should.ThrowAsync<InvalidOperationException>(answer)).Message.ShouldBe("no");
        owner.Close();
    }
}
