using AnimationEditor.Core.HotReload;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Another program editing the open project's files: the .achx rewritten on disk, its PNG
/// replaced, and Reload From Disk.
/// </summary>
public class ExternalChangeScenarioTests
{
    private static readonly TimeSpan HotReloadTimeout = TimeSpan.FromSeconds(5);

    [AvaloniaFact]
    public async Task AchxRewrittenOnDisk_HotReloadsTheTree()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.Services.AppCommands.HotReloadWatcher.IsEnabled.ShouldBeTrue("hot reload is on by default");

        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));

        editor.WaitUntil(() => editor.Project.AnimationChains.Count == 2, HotReloadTimeout)
            .ShouldBeTrue("the editor should pick up the rewritten file");
        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Walk", "Run" });
        editor.ThrowIfErrorShown();
    }

    [AvaloniaFact]
    public async Task AchxRewrittenOnDisk_WithHotReloadOff_LeavesTheEditorAlone_UntilReloadFromDisk()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickMenu("MenuEnableHotReload");
        editor.Services.AppCommands.HotReloadWatcher.IsEnabled.ShouldBeFalse();

        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));
        editor.Wait(TimeSpan.FromMilliseconds(600));

        editor.Project.AnimationChains.Count.ShouldBe(1, "hot reload is off");

        editor.ClickMenu("MenuReloadFromDisk");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
    }

    // The history's commands hold objects of the model the reload replaced, so undoing one would change nothing shown.
    [AvaloniaFact]
    public async Task SwitchingBackToATabWhoseFileChanged_ReloadsItWithoutItsOldHistory()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = editor.WriteAchx("boss.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(hero);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.Delete);
        editor.UndoLabels.Count.ShouldBe(1);
        await editor.OpenAsync(boss);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));

        editor.ClickTab("hero.achx");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    // The achx stays the loaded document, and watched, behind a .seqj or .png tab.
    [AvaloniaTheory]
    [InlineData("boss.seqj", false)]
    [InlineData("boss.seqj", true)]
    [InlineData("boss.png", false)]
    [InlineData("boss.png", true)]
    public async Task AchxRewrittenOnDisk_BehindASeqjOrPngTab_IsSeenOnSwitchBack(string front, bool failedSave)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string boss = front.EndsWith("png") ? editor.WritePng(front, 64, 64) : editor.WriteSeqj(front, "Idle");
        await editor.OpenAsync(hero);
        editor.Services.AppCommands.HotReloadWatcher.IsEnabled = false;
        var watcher = new DrivenWatcher();
        editor.Services.AppCommands.HotReloadWatcher = watcher;
        editor.Services.AppCommands.WireHotReloadWatcher();
        if (failedSave)
        {
            UnusualProjectScenarioTests.MakeUnwritable(hero);
            editor.ClickRow(editor.ChainNamed("Walk"));
            editor.Press(Key.Delete);
            UnusualProjectScenarioTests.MakeWritable(hero);
            editor.ProblemText!.ShouldContain("Auto save failed");
        }
        await editor.OpenAsync(boss);
        await editor.Window.WhenPngTabLoaded();
        if (front.EndsWith("seqj"))
        {
            editor.Click(editor.Control<Button>("AddSequenceButton"));
            editor.Wait(TimeSpan.FromMilliseconds(100));
            editor.Press(Key.Enter);
        }
        List<string> undo = editor.UndoLabels;
        string? info = editor.InfoText, problem = editor.ProblemText;
        AnimationChainListSave? loaded = editor.Services.ProjectManager.AnimationChainListSave;
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Run", "sheet.png", (16, 0, 16, 16)));

        watcher.RaiseAchxChanged(hero);
        editor.Wait(TimeSpan.FromMilliseconds(50));

        editor.Services.ProjectManager.AnimationChainListSave.ShouldBeSameAs(loaded);
        editor.UndoLabels.ShouldBe(undo);
        editor.InfoText.ShouldBe(info);
        editor.ProblemText.ShouldBe(problem);

        editor.ClickTab("hero.achx");
        editor.Wait(TimeSpan.FromMilliseconds(100));
        editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(failedSave ? Array.Empty<string>() : new[] { "Run" });
        (editor.ProblemText?.Contains("\"hero.achx\" changed on disk") == true).ShouldBe(failedSave);
    }

    private sealed class DrivenWatcher : IHotReloadWatcher
    {
        public event Action<string>? AchxChangedOnDisk;
        public event Action<string>? PngChangedOnDisk { add { } remove { } }
        public event Action<string>? AchxDeletedOnDisk { add { } remove { } }
        public event Action<string>? SourceChangedOnDisk { add { } remove { } }

        public bool IsEnabled { get; set; } = true;

        public void RaiseAchxChanged(string path) => AchxChangedOnDisk?.Invoke(path);

        public void StartWatching(string achxPath, IEnumerable<string> pngPaths) { }
        public void UpdatePngList(IEnumerable<string> newPngPaths) { }
        public void UpdateSourceList(IEnumerable<string> sourcePaths) { }
        public void StopWatching() { }
        public void RunOwnSave(string filePath, Action write) => write();
        public void Dispose() { }
    }

    [AvaloniaFact]
    public async Task OwnSave_DoesNotTriggerAReload_ThatDropsTheSelection()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.ClickRow(walk);

        editor.Press(Key.S, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(600));

        editor.Services.SelectedState.SelectedChain.ShouldBeSameAs(walk, "saving must not reload the project out from under the selection");
        editor.ChainNamed("Walk").ShouldBeSameAs(walk);
    }

    [AvaloniaFact]
    public async Task PngReplacedOnDisk_ReloadsTheWireframeTexture()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Wireframe.BitmapSize.ShouldBe((64, 64));

        editor.WritePng("sheet.png", 128, 96, SKColors.Red);

        editor.WaitUntil(() => editor.Wireframe.BitmapSize == (128, 96), HotReloadTimeout)
            .ShouldBeTrue("the wireframe should reload the replaced PNG");
    }
}
