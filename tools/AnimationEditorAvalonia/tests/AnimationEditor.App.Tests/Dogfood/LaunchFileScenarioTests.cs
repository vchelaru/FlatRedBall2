using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>The editor started with a file, as a double-click in Explorer does.</summary>
public class LaunchFileScenarioTests
{
    private static string WriteAchx(string folder, string name, string chain)
    {
        string path = Path.Combine(folder, name);
        new AnimationChainListSave
        {
            CoordinateType = TextureCoordinateType.Pixel,
            AnimationChains = { new AnimationChainSave { Name = chain } },
        }.Save(path);
        return path;
    }

    /// <summary>Starts the editor on <paramref name="file"/> after a session that had boss.achx in front and hero.achx open.</summary>
    private static void Launch(string file, bool crashed, Action<TestServices, MainWindow, TabManager> check)
    {
        string folder = Directory.CreateTempSubdirectory().FullName;
        TestServices services = TestHelpers.BuildServices();
        MainWindow? window = null;
        try
        {
            string boss = WriteAchx(folder, "boss.achx", "Roar");
            string hero = WriteAchx(folder, "hero.achx", "Walk");
            WriteAchx(folder, "enemy.achx", "Run");
            var settings = AppSettingsLocation.ForApplicationDataRoot(services.SettingsRoot);
            Directory.CreateDirectory(settings.GetDirectoryContainingThis().FullPath);
            File.WriteAllText(settings.FullPath, JsonSerializer.Serialize(new AppSettingsModel { OpenTabPaths = [boss, hero], ActiveTabPath = boss }));
            if (crashed)
                services.IoManager.WriteRecoveryFile(new AnimationChainListSave { AnimationChains = { new AnimationChainSave { Name = "Unsaved" } } });

            window = services.CreateMainWindow();
            window.LaunchFile = Path.Combine(folder, file);
            window.Show();
            Dispatcher.UIThread.RunJobs();

            check(services, window, (TabManager)typeof(MainWindow)
                .GetField("_tabManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(window)!);
        }
        finally
        {
            window?.CloseWithoutPrompt();
            Directory.Delete(folder, recursive: true);
        }
    }

    [AvaloniaFact]
    public void Launch_WithAFile_RestoresTheTabsAndOpensItInFront() => Launch("enemy.achx", crashed: false, (services, window, tabs) =>
    {
        tabs.Tabs.Select(tab => tab.DisplayName).ShouldBe(new[] { "boss.achx", "hero.achx", "enemy.achx" });
        tabs.ActiveTab!.DisplayName.ShouldBe("enemy.achx");
        services.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Name.ShouldBe("Run");

        window.CloseWithoutPrompt();
        JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(AppSettingsLocation.ForApplicationDataRoot(services.SettingsRoot).FullPath))!
            .OpenTabPaths.Select(path => Path.GetFileName(path)).ShouldBe(new[] { "boss.achx", "hero.achx", "enemy.achx" });
    });

    [AvaloniaFact]
    public void Launch_WithAFileThatWasOpen_BringsItsTabToTheFront() => Launch("hero.achx", crashed: false, (services, _, tabs) =>
    {
        tabs.Tabs.Select(tab => tab.DisplayName).ShouldBe(new[] { "boss.achx", "hero.achx" });
        tabs.ActiveTab!.DisplayName.ShouldBe("hero.achx");
        services.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Name.ShouldBe("Walk");
    });

    [AvaloniaFact]
    public void Launch_WithAFileAfterACrash_RecoversTheDocumentBehindIt() => Launch("enemy.achx", crashed: true, (services, _, tabs) =>
    {
        tabs.Tabs.Select(tab => tab.DisplayName).ShouldBe(new[] { "boss.achx", "hero.achx", "Untitled", "enemy.achx" });
        tabs.ActiveTab!.DisplayName.ShouldBe("enemy.achx");
        services.ProjectManager.AnimationChainListSave!.AnimationChains.Single().Name.ShouldBe("Run");
        tabs.Tabs[2].CachedEditorModel!.AnimationChains.Single().Name.ShouldBe("Unsaved");
    });
}
