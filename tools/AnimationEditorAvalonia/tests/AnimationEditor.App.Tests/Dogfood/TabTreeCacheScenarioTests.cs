using AnimationEditor.Core.Models;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Each tab keeps its own animation tree. Switching back to a tab shows that tree again instead of
/// clearing and rebuilding it, which was most of the cost of a tab switch.
/// </summary>
public class TabTreeCacheScenarioTests
{
    private static async Task<AnimationEditorHarness> TwoTabsAsync(Func<AnimationEditorHarness, (string Hero, string Enemy)> write)
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        (string hero, string enemy) = write(editor);
        await editor.OpenAsync(enemy);
        await editor.OpenAsync(hero);
        return editor;
    }

    private static (string, string) WriteBoth(AnimationEditorHarness editor) => (
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16))),
        editor.WriteAchx("enemy.achx",
            AnimationEditorHarness.Chain("Bite", "sheet.png", (0, 0, 16, 16))));

    [AvaloniaFact]
    public async Task SwitchingBackToATab_ShowsTheSameTreeAndNodes_NotARebuild()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        TreeView heroTree = editor.AnimTree;
        TreeNodeVm walkNode = editor.Nodes.First(node => node.Header == "Walk");

        editor.ClickTab("enemy.achx");
        TreeView enemyTree = editor.AnimTree;
        editor.ClickTab("hero.achx");

        enemyTree.ShouldNotBeSameAs(heroTree, "each tab has its own tree");
        editor.AnimTree.ShouldBeSameAs(heroTree);
        editor.Nodes.First(node => node.Header == "Walk").ShouldBeSameAs(walkNode, "the node view-models were kept, not rebuilt");
        heroTree.IsVisible.ShouldBeTrue();
        enemyTree.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task EachTabShowsItsOwnChains_AcrossManySwitches()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);

        for (int i = 0; i < 3; i++)
        {
            editor.ClickTab("enemy.achx");
            editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Bite" });
            editor.ClickTab("hero.achx");
            editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Walk", "Run" });
        }
    }

    [AvaloniaFact]
    public async Task ExpandedFrames_SurviveSwitchingAwayAndBack()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        AnimationChainSave walk = editor.ChainNamed("Walk");
        editor.Expand(walk);
        editor.NodeFor(walk).IsExpanded.ShouldBeTrue();

        editor.ClickTab("enemy.achx");
        editor.ClickTab("hero.achx");

        editor.NodeFor(editor.ChainNamed("Walk")).IsExpanded.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task SelectionOfEachTab_IsKeptAcrossSwitches()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        editor.ClickRow(editor.ChainNamed("Run"));

        editor.ClickTab("enemy.achx");
        editor.ClickTab("hero.achx");

        editor.AnimTree.SelectedItems.OfType<TreeNodeVm>().Select(node => node.Data)
            .ShouldBe(new object[] { editor.ChainNamed("Run") });
    }

    [AvaloniaFact]
    public async Task ARemovedChainInABackgroundDocument_IsGoneWhenYouSwitchBack()
    {
        // A cross-tab cut removes chains from the source tab's document while another tab is
        // active. The source tab's kept tree must not show the chain that is no longer there.
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        editor.ClickTab("enemy.achx");
        TabEntry heroTab = editor.Tabs.Tabs.Single(tab => tab.Path.FullPath.EndsWith("hero.achx"));
        heroTab.CachedEditorModel!.AnimationChains.RemoveAll(chain => chain.Name == "Walk");

        editor.ClickTab("hero.achx");

        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Run" });
    }

    [AvaloniaFact]
    public async Task ReloadingTheActiveFromDisk_StillRebuildsItsTree()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Sprint", "sheet.png", (0, 0, 16, 16)));

        editor.ClickMenu("MenuReloadFromDisk");
        editor.Wait(TimeSpan.FromMilliseconds(100));

        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Sprint" });
    }

    [AvaloniaFact]
    public async Task ClosingATab_DiscardsItsTree()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        Panel host = editor.Control<Panel>("AnimTreeHost");
        int before = host.Children.Count;

        editor.CloseTab("enemy.achx");

        host.Children.Count.ShouldBe(before - 1);
        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Walk", "Run" });
    }

    [AvaloniaFact]
    public async Task EditingAfterSwitchingBack_WorksInTheKeptTree_AndUndoRestoresIt()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        editor.ClickTab("enemy.achx");
        editor.ClickTab("hero.achx");

        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Press(Key.Delete);

        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Walk" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Walk", "Run" });
    }

    [AvaloniaFact]
    public async Task EditsMadeInOneTab_AreInItsTreeWhenYouComeBack_AndNotInTheOthers()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Wait(TimeSpan.FromMilliseconds(20));
        editor.Type("Jump");
        editor.Press(Key.Enter);

        editor.ClickTab("enemy.achx");
        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Bite" });
        editor.ClickTab("hero.achx");

        editor.Nodes.Where(node => node.IsChainNode).Select(node => node.Header).ShouldBe(new[] { "Walk", "Run", "Jump" });
    }

    [AvaloniaFact]
    public async Task TheFilter_AppliesToAKeptTree_WhenItsTabIsShownAgain()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        editor.ClickTab("enemy.achx");
        editor.Click(editor.AnimSearch.SearchToggleBtn);
        editor.TypeAndEnter(editor.AnimSearch.SearchBox, "ru");

        editor.ClickTab("hero.achx");

        // The filter was typed while the hero tree was hidden: Run matches, Walk is the selected row.
        editor.Nodes.First(node => node.Header == "Run").PinnedVisible.ShouldBeTrue();
        editor.Nodes.First(node => node.Header == "Walk").PinnedVisible.ShouldBeFalse();

        editor.Click(editor.AnimSearch.SearchClearBtn);
        editor.Nodes.Where(node => node.IsChainNode).All(node => node.PinnedVisible).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task ExactlyOneTreeIsVisible_AndItIsTheOneFoundByName()
    {
        using AnimationEditorHarness editor = await TwoTabsAsync(WriteBoth);
        editor.ClickTab("enemy.achx");

        Panel host = editor.Control<Panel>("AnimTreeHost");
        host.Children.OfType<TreeView>().Count(tree => tree.IsVisible).ShouldBe(1);
        host.Children.OfType<TreeView>().Single(tree => tree.IsVisible).ShouldBeSameAs(editor.AnimTree);
    }
}
