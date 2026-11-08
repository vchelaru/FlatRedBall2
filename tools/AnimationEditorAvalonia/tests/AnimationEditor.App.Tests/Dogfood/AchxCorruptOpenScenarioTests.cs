using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// An <c>.achx</c> that no longer parses when its tab is shown (a tab switch, or session restore) is dropped with the
/// error banner, like the <c>.seqj</c> case in <see cref="SequenceMalformedOpenScenarioTests"/>: no ghost tab, and
/// whichever loadable tab is left shows.
/// </summary>
public class AchxCorruptOpenScenarioTests
{
    private const string Corrupt = "<AnimationChainArrayS";

    private static void ShouldShowTheBannerFor(AnimationEditorHarness editor, string file)
    {
        editor.ProblemText.ShouldNotBeNull();
        editor.ProblemText!.ShouldContain(file);
    }

    private static void WriteFixtures(string folder)
    {
        File.WriteAllText(Path.Combine(folder, "broken.achx"), Corrupt);
        // Empty but valid: opens with no PNG to resolve.
        new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }.Save(Path.Combine(folder, "good.achx"));
    }

    [AvaloniaFact]
    public async Task SwitchingToATabWhoseAchxNowFailsToLoad_ShowsTheBanner_DropsIt_AndKeepsTheTabYouCameFrom()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Roar", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(boss);
        await editor.OpenAsync(hero);
        // A write after the tab was left makes its cache stale, so the switch reloads from disk.
        await editor.WaitAsync(TimeSpan.FromMilliseconds(50));
        File.WriteAllText(boss, Corrupt);
        File.SetLastWriteTimeUtc(boss, DateTime.UtcNow.AddMinutes(1));

        editor.ClickTab("boss.achx");
        editor.Layout();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        ShouldShowTheBannerFor(editor, "boss.achx");
        editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public void Restoring_AnAchxTabBehindTheActiveOne_ThatFailsToLoad_ShowsTheBannerWhenSelected_AndDropsIt()
    {
        SequenceMalformedOpenScenarioTests.RunRestore(
            folder => (new[] { Path.Combine(folder, "good.achx"), Path.Combine(folder, "broken.achx") }, Path.Combine(folder, "good.achx")),
            editor =>
            {
                editor.WaitUntil(() => editor.TabLabels.Count == 2, TimeSpan.FromSeconds(5));
                editor.Layout();
                editor.ProblemText.ShouldBeNull();

                editor.ClickTab("broken.achx");
                editor.Layout();

                ShouldShowTheBannerFor(editor, "broken.achx");
                editor.TabLabels.ShouldBe(new[] { "good.achx" });
                editor.Tabs.ActiveTab!.DisplayName.ShouldBe("good.achx");
            },
            WriteFixtures);
    }

    [AvaloniaFact]
    public void Restoring_ASessionWhoseActiveAchxFailsToLoad_ShowsTheBanner_AndShowsTheOtherTab()
    {
        SequenceMalformedOpenScenarioTests.RunRestore(
            folder => (new[] { Path.Combine(folder, "broken.achx"), Path.Combine(folder, "good.achx") }, Path.Combine(folder, "broken.achx")),
            editor =>
            {
                editor.WaitUntil(() => editor.ProblemText != null, TimeSpan.FromSeconds(5));
                editor.Layout();

                ShouldShowTheBannerFor(editor, "broken.achx");
                editor.TabLabels.ShouldBe(new[] { "good.achx" });
                editor.Tabs.ActiveTab!.DisplayName.ShouldBe("good.achx");
            },
            WriteFixtures);
    }

    [AvaloniaFact]
    public void Restoring_ASessionWhoseOnlyTabIsACorruptAchx_ShowsTheBanner_AndOpensNoTab()
    {
        SequenceMalformedOpenScenarioTests.RunRestore(
            folder => (new[] { Path.Combine(folder, "broken.achx") }, Path.Combine(folder, "broken.achx")),
            editor =>
            {
                editor.WaitUntil(() => editor.ProblemText != null, TimeSpan.FromSeconds(5));
                editor.Layout();

                ShouldShowTheBannerFor(editor, "broken.achx");
                editor.TabLabels.ShouldBeEmpty();
            },
            WriteFixtures);
    }
}
