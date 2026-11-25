using Avalonia.Headless.XUnit;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>An .achx saved the way other editors save: the old file moved away, the new one renamed into place.</summary>
public class ReplaceByRenameScenarioTests
{
    [AvaloniaFact]
    public async Task AchxReplacedThroughABackupAndARename_AsOtherEditorsSave_Reloads()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (8, 8, 16, 16)));
        await editor.OpenAsync(hero);
        string next = editor.WriteAchx("hero.tmp", AnimationEditorHarness.Chain("Run", "sheet.png", (8, 8, 16, 16)));

        File.Move(hero, hero + ".bak");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(400));    // slower than the watcher's window for one save
        File.Move(next, hero);
        await editor.WaitUntilAsync(() => editor.Project.AnimationChains[0].Name == "Run", TimeSpan.FromSeconds(8));

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run" });
    }
}
