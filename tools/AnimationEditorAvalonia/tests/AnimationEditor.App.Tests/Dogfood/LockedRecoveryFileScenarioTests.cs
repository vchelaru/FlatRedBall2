using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>Files the editor keeps for itself, or reads at startup, that are damaged, locked or half written.</summary>
public class LockedRecoveryFileScenarioTests
{
    private static string WriteSettings(string folder, string json)
    {
        string root = Path.Combine(folder, "settings");
        var file = AppSettingsLocation.ForApplicationDataRoot(root);
        Directory.CreateDirectory(file.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(file.FullPath, json);
        return root;
    }

    private static string WriteHero(string folder)
    {
        string hero = Path.Combine(folder, "hero.achx");
        new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }.Save(hero);
        return hero;
    }

    private static void InTempFolder(Action<string> test)
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        try { test(folder); }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [AvaloniaFact]
    public void Startup_WithARecoveryFileThatCannotBeDeleted_StillRestoresTheTabs() => InTempFolder(folder =>
    {
        string hero = WriteHero(folder);
        string settings = WriteSettings(folder, JsonSerializer.Serialize(new AppSettingsModel { OpenTabPaths = [hero], ActiveTabPath = hero }));
        string recovery = Path.Combine(folder, "recovery.achx");
        new AnimationChainListSave { AnimationChains = { new AnimationChainSave { Name = "Recovered" } } }.Save(recovery);
        // A virus scanner or a backup reading it. Elsewhere than Windows an open file can be deleted.
        using FileStream held = new FileStream(recovery, FileMode.Open, FileAccess.Read, FileShare.Read);

        using AnimationEditorHarness editor = new AnimationEditorHarness(settings, recovery);
        editor.WaitUntil(() => editor.TabLabels.Count == 2, TimeSpan.FromSeconds(3));

        List<string> tabs = editor.TabLabels.ToList();
        editor.Window.CloseWithoutPrompt();
        tabs.ShouldBe(new[] { "hero.achx", "Untitled" });
    });

    [AvaloniaFact]
    public void Startup_WithARecoveryFileThatCannotBeRead_RestoresTheTabs_SaysSo_AndKeepsTheFile() => InTempFolder(folder =>
    {
        string hero = WriteHero(folder);
        string settings = WriteSettings(folder, JsonSerializer.Serialize(new AppSettingsModel { OpenTabPaths = [hero], ActiveTabPath = hero }));
        string recovery = Path.Combine(folder, "recovery.achx");
        new AnimationChainListSave { AnimationChains = { new AnimationChainSave { Name = "Recovered" } } }.Save(recovery);
        using (new FileStream(recovery, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settings, recovery);
            editor.WaitUntil(() => editor.ProblemText is not null, TimeSpan.FromSeconds(3));

            editor.TabLabels.ShouldBe(new[] { "hero.achx" });
            editor.ProblemText.ShouldNotBeNull().ShouldContain("Recovering an unsaved document failed");
            editor.Window.CloseWithoutPrompt();
        }

        using AnimationEditorHarness next = new AnimationEditorHarness(settings, recovery);
        next.WaitUntil(() => next.TabLabels.Count == 2, TimeSpan.FromSeconds(3));
        List<string> tabs = next.TabLabels.ToList();
        next.Window.CloseWithoutPrompt();
        tabs.ShouldBe(new[] { "hero.achx", "Untitled" });
    });
}
