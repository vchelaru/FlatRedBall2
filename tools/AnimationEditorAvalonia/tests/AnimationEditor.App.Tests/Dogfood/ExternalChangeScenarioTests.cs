using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using SkiaSharp;

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
