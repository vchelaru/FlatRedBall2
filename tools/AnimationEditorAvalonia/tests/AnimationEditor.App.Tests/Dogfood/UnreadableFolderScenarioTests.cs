using AnimationEditor.Core.IO;
using AnimationEditor.App.Services;
using AnimationEditor.Tests.Shared;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>A subfolder the user may not list (every Windows drive root has one) beside an animation file.</summary>
public class UnreadableFolderScenarioTests
{
    [AvaloniaFact]
    public async Task OpeningAFile_BesideAnUnreadableFolder_Opens()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        using var denied = new UnlistableFolder(Path.Combine(editor.ProjectFolder, "denied"));

        await editor.OpenAsync(hero);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(100));

        editor.TabLabels.ShouldBe(new[] { "hero.achx" });
        editor.VisibleChainHeaders.ShouldBe(new[] { "Walk" });
    }

    [AvaloniaFact]
    public async Task OpeningAProjectFolder_HoldingAnUnreadableFolder_ListsTheRest()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WriteAchx("hero.achx");
        using var denied = new UnlistableFolder(Path.Combine(editor.ProjectFolder, "denied"));

        var files = await AchxFolderScanner.ScanAsync(new DiskEditorFolder(editor.ProjectFolder));

        files.Select(file => file.RelativePath).ShouldBe(new[] { "hero.achx" });
    }

    [AvaloniaFact]
    public async Task OpeningATileset_BesideAnUnreadableFolder_Opens()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string tsx = Path.Combine(editor.ProjectFolder, "Heroes.tsx");
        File.WriteAllText(tsx, """
            <?xml version="1.0" encoding="UTF-8"?>
            <tileset version="1.10" tiledversion="1.10.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
             <image source="sheet.png" width="64" height="64"/>
            </tileset>
            """);
        using var denied = new UnlistableFolder(Path.Combine(editor.ProjectFolder, "denied"));

        await editor.OpenAsync(tsx);

        editor.ProblemText.ShouldBeNull();
        editor.TabLabels.ShouldBe(new[] { "Heroes.tsx" });
    }

    [AvaloniaFact]
    public async Task OpeningAProjectFolder_HoldingAnUnreadableFolder_ShowsItsFiles()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        using var denied = new UnlistableFolder(Path.Combine(editor.ProjectFolder, "denied"));

        await editor.OpenProjectFolderAsync();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.Control<ProjectPanelControl>("ProjectPanel").TreeRoots.Select(node => node.Name).ShouldContain("hero.achx");
    }

    /// <summary>On Windows a folder the user may not list cannot be watched, so opening it fails.</summary>
    [AvaloniaFact]
    public async Task OpenRecent_OnAnUnreadableFolder_ShowsTheProblem()
    {
        if (!OperatingSystem.IsWindows()) return;
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string folder = Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "denied")).FullName;
        await editor.OpenProjectFolderAsync(folder);
        await editor.OpenProjectFolderAsync(editor.ProjectFolder);
        using var denied = new UnlistableFolder(folder);

        editor.Control<MenuItem>("MenuLoadRecent").Items.OfType<MenuItem>().Single(item => item.Header!.ToString()!.Contains("denied"))
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(200));

        editor.ProblemText.ShouldNotBeNull().ShouldContain("Open Project Folder failed");
    }

    /// <summary>On Linux the watcher reports an error for the subfolder each time it starts.</summary>
    [AvaloniaFact]
    public async Task RescanningAProjectFolder_HoldingAnUnreadableFolder_Ends()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WriteAchx("hero.achx");
        using var denied = new UnlistableFolder(Path.Combine(editor.ProjectFolder, "denied"));
        await editor.OpenProjectFolderAsync();
        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));
        Task last = editor.Window.LastProjectFolderWatcherHandledTask;

        await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

        editor.Window.LastProjectFolderWatcherHandledTask.ShouldBeSameAs(last);
    }
}
