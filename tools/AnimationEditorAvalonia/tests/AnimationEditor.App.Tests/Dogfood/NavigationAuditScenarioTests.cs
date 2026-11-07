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
