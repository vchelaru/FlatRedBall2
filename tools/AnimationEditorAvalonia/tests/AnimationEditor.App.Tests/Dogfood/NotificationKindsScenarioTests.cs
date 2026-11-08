using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The two pop-up kinds as a user meets them. INFO needs nothing from the user: top-right toast, auto-hides.
/// PROBLEM means work is not saved or an asked action failed: top-centre banner, dismissable.
/// </summary>
public class NotificationKindsScenarioTests
{
    [AvaloniaFact]
    public async Task Info_AppearsTopRight_AndAutoHides()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = editor.WriteSeqj("boss.seqj", "Attack");
        await editor.OpenAsync(seqj);
        editor.ClickMenu("MenuEnableHotReload");
        var save = new SequenceListSave();
        save.Sequences.Add(new SequenceSave { Name = "Intro" });
        save.Save(seqj);

        editor.ClickMenu("MenuReloadFromDisk");

        editor.InfoText.ShouldNotBeNull().ShouldContain("Reloaded boss.seqj");
        editor.ProblemText.ShouldBeNull();
        editor.Notifications.ToastPanel.HorizontalAlignment.ShouldBe(HorizontalAlignment.Right);
        editor.Notifications.ToastPanel.VerticalAlignment.ShouldBe(VerticalAlignment.Top);
        (await editor.WaitUntilAsync(() => editor.InfoText == null, TimeSpan.FromSeconds(9))).ShouldBeTrue("about 6 s");
    }

    [AvaloniaFact]
    public async Task Problem_AppearsTopCentre_AndDismisses()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", "Sequence");
        await editor.OpenAsync(boss);
        UnusualProjectScenarioTests.MakeUnwritable(boss);
        try
        {
            editor.Click(editor.Control<Button>("AddSequenceButton"));

            editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Auto save failed");
            editor.InfoText.ShouldBeNull();
            editor.Notifications.ErrorBanner.HorizontalAlignment.ShouldBe(HorizontalAlignment.Center);
            editor.Notifications.ErrorBanner.VerticalAlignment.ShouldBe(VerticalAlignment.Top);

            editor.Click(editor.Notifications.ErrorBannerDismissBtn);

            editor.ProblemText.ShouldBeNull();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(boss);
        }
    }

    [AvaloniaFact]
    public async Task FailedAutoSave_OnAnAchxTab_IsAProblemBanner_NotAToast()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.ClickRow(walk.Frames[0]);
        UnusualProjectScenarioTests.MakeUnwritable(path);
        try
        {
            editor.TypeNumber("PropPixelX", "8");

            editor.ProblemText.ShouldNotBeNull().ShouldContain("Auto save failed");
            editor.InfoText.ShouldBeNull();
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(path);
        }
    }

    [AvaloniaFact]
    public async Task RefusedPaste_IsAProblemBanner()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = editor.WriteSeqj("boss.seqj", "Attack", "Idle");
        await editor.OpenAsync(seqj);
        await editor.Window.Clipboard!.ClearAsync();

        editor.Press(Avalonia.Input.Key.V, Avalonia.Input.RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        editor.ProblemText.ShouldNotBeNull().ShouldContain("Nothing to paste");
        editor.InfoText.ShouldBeNull();
    }
}
