using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Navigation-history behaviors the first rounds left unpinned: Redo and Ctrl chords record nothing, a sequence that
/// no longer exists is skipped by Back, and which keys count as navigation input.
/// </summary>
public class NavigationAuditScenarioTests
{
    private static void Back(AnimationEditorHarness editor) => editor.Press(Key.Left, RawInputModifiers.Alt);

    private static async Task SettleAsync(AnimationEditorHarness editor) => await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

    private static bool BackEnabled(AnimationEditorHarness editor) => editor.Control<MenuItem>("MenuNavBack").IsEnabled;

    [AvaloniaFact]
    public async Task DuplicateThenUndoAndRedo_RecordNothing_SoBackGoesStraightToThePreviousChain()
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Run"));
        string? SelectedChain() => editor.Services.SelectedState.SelectedChain?.Name;

        // Ctrl+D is a chord: it is a command, not a navigation, and it moves the selection to the copy.
        editor.Press(Key.D, RawInputModifiers.Control);
        string copy = SelectedChain()!;
        copy.ShouldNotBe("Run");
        editor.ClickMenu("MenuUndo");
        await SettleAsync(editor);
        SelectedChain().ShouldBe("Run");
        editor.ClickMenu("MenuRedo");
        await SettleAsync(editor);
        SelectedChain().ShouldBe(copy);

        // Only the click on Run was recorded, so one Back reaches Walk and leaves nothing behind it.
        Back(editor);
        await SettleAsync(editor);
        SelectedChain().ShouldBe("Walk");
        BackEnabled(editor).ShouldBeFalse();
    }

    // Option+Left/Right move the caret by a word in a Mac text box; Back is for everywhere else. On Windows and Linux
    // Alt+Arrow means nothing to a text box, so Back stays available there.
    [AvaloniaTheory]
    [InlineData(true, "Run")]
    [InlineData(false, "Walk")]
    public async Task AltLeft_InATextBox_IsBackOnlyOffTheMac(bool mac, string chainAfterAltLeft)
    {
        using var editor = new AnimationEditorHarness(commandModifier: mac ? CommandModifier.Meta : CommandModifier.Control);
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Run"));
        BackEnabled(editor).ShouldBeTrue();
        TextBox box = editor.Control<TextBox>("PropChainName");
        box.Focus();
        editor.Layout();

        Back(editor);
        await SettleAsync(editor);

        editor.Services.SelectedState.SelectedChain?.Name.ShouldBe(chainAfterAltLeft);
    }

    // The Mac menu cannot show Option+Left/Right (AppKit would hand it to Back even inside a text box), so Back and
    // Forward are Cmd+[ and Cmd+] there, as in Finder and browsers, and work from a text box too.
    [AvaloniaFact]
    public async Task OnTheMac_BackAndForwardAreCmdBrackets_EvenInATextBox_AndTheBarShowsThem()
    {
        using var editor = new AnimationEditorHarness(commandModifier: CommandModifier.Meta);
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Control<TextBox>("PropChainName").Focus();
        editor.Layout();
        string? Selected() => editor.Services.SelectedState.SelectedChain?.Name;

        editor.Press(Key.OemOpenBrackets, RawInputModifiers.Meta);
        await SettleAsync(editor);
        Selected().ShouldBe("Walk");

        editor.Press(Key.OemCloseBrackets, RawInputModifiers.Meta);
        await SettleAsync(editor);
        Selected().ShouldBe("Run");

        NativeMenu bar = NativeMenuTestSupport.Build(editor);
        NativeMenuTestSupport.Find(bar, "View", "Back").Gesture.ShouldBe(new KeyGesture(Key.OemOpenBrackets, KeyModifiers.Meta));
        NativeMenuTestSupport.Find(bar, "View", "Forward").Gesture.ShouldBe(new KeyGesture(Key.OemCloseBrackets, KeyModifiers.Meta));
    }

    [AvaloniaFact]
    public async Task Back_PastASequenceThatWasUndoneAway_SkipsIt()
    {
        using var editor = new AnimationEditorHarness();
        string path = editor.WriteSeqj("boss.seqj", "Idle");
        await editor.OpenAsync(path);
        var list = editor.Control<ListBox>("SequenceList");

        // Adding selects the new sequence 1 (recording Idle); clicking Idle again records sequence 1.
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        list.SelectedIndex.ShouldBe(1);
        editor.Click((Control)list.ContainerFromIndex(0)!);
        editor.Press(Key.Z, RawInputModifiers.Control);
        list.ItemCount.ShouldBe(1);

        // Sequence 1 is gone: Back discards it, lands on Idle, and has nothing older to offer.
        Back(editor);
        await SettleAsync(editor);

        list.SelectedIndex.ShouldBe(0);
        BackEnabled(editor).ShouldBeFalse();
    }

    [Fact]
    public void IsNavigationInputKey_CountsPlainKeys_ButNotChordsOrLoneModifiers()
    {
        MainWindow.IsNavigationInputKey(Key.Down, KeyModifiers.None).ShouldBeTrue();
        MainWindow.IsNavigationInputKey(Key.Down, KeyModifiers.Shift).ShouldBeTrue();
        MainWindow.IsNavigationInputKey(Key.Z, KeyModifiers.Control).ShouldBeFalse();
        MainWindow.IsNavigationInputKey(Key.Z, KeyModifiers.Meta).ShouldBeFalse();
        MainWindow.IsNavigationInputKey(Key.Left, KeyModifiers.Alt).ShouldBeFalse();
        foreach (var modifier in new[] { Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin })
            MainWindow.IsNavigationInputKey(modifier, KeyModifiers.None).ShouldBeFalse(modifier.ToString());
    }
}
