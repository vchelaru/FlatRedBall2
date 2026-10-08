using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Issue #1360: File &gt; Close Project Folder must close only the tabs whose file lives under the
/// project folder, leaving files opened from elsewhere (and Untitled tabs) alone.
/// </summary>
public class TabManagerCloseTabsInFolderTests
{
    private static FilePath P(string path) => new FilePath(path);

    private static readonly FilePath Folder = P(@"C:\Games\Project");

    [Fact]
    public void CloseTabsInFolder_ClosesTabsUnderFolder_KeepsTabsOutsideIt()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Elsewhere\outside.achx"));
        tm.OpenOrFocus(P(@"C:\Games\Project\hero.achx"));
        tm.OpenOrFocus(P(@"C:\Games\Project\sub\enemy.achx"));

        tm.CloseTabsInFolder(Folder);

        Assert.Equal(new[] { P(@"C:\Elsewhere\outside.achx") }, tm.Tabs.Select(t => t.Path));
    }

    [Fact]
    public void CloseTabsInFolder_ReturnsTheClosedTabs()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Elsewhere\outside.achx"));
        tm.OpenOrFocus(P(@"C:\Games\Project\hero.achx"));

        var closed = tm.CloseTabsInFolder(Folder);

        Assert.Equal(new[] { P(@"C:\Games\Project\hero.achx") }, closed.Select(t => t.Path));
    }

    // A sibling folder that merely starts with the same characters is not inside the folder.
    [Fact]
    public void CloseTabsInFolder_SiblingFolderSharingNamePrefix_IsKept()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\Project2\other.achx"));

        tm.CloseTabsInFolder(Folder);

        Assert.Single(tm.Tabs);
    }

    [Fact]
    public void CloseTabsInFolder_MixedSeparatorsAndCasing_StillMatches()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P("c:/games/PROJECT/Hero.achx"));

        tm.CloseTabsInFolder(Folder);

        Assert.Empty(tm.Tabs);
    }

    [Fact]
    public void CloseTabsInFolder_PngTabUnderFolder_IsClosed()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\Project\sheet.png"));

        tm.CloseTabsInFolder(Folder);

        Assert.Empty(tm.Tabs);
    }

    [Fact]
    public void CloseTabsInFolder_UntitledTabs_AreKept()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(tm.NewUntitledSentinelPath()), "Untitled");
        tm.OpenOrFocus(P(@"C:\Games\Project\hero.achx"));

        tm.CloseTabsInFolder(Folder);

        var survivor = Assert.Single(tm.Tabs);
        Assert.Equal("Untitled", survivor.DisplayName);
    }

    [Fact]
    public void CloseTabsInFolder_ActiveTabOutsideFolder_StaysActiveAndRaisesNoActiveChange()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\Project\hero.achx"));
        tm.OpenOrFocus(P(@"C:\Elsewhere\outside.achx"));
        int activeChanges = 0;
        tm.ActiveChanged += _ => activeChanges++;

        tm.CloseTabsInFolder(Folder);

        Assert.Equal(P(@"C:\Elsewhere\outside.achx"), tm.ActiveTab!.Path);
        Assert.Equal(0, activeChanges);
    }

    // The most recently used tab before the active one is itself in the folder; the survivor must
    // still win, so closing order cannot leave a just-closed folder tab as the active one.
    [Fact]
    public void CloseTabsInFolder_ActiveTabInFolder_ActivatesASurvivorNotAClosedTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Elsewhere\outside.achx"));
        tm.OpenOrFocus(P(@"C:\Games\Project\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\Project\b.achx")); // active; MRU predecessor is a.achx (in folder)

        tm.CloseTabsInFolder(Folder);

        Assert.Equal(P(@"C:\Elsewhere\outside.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void CloseTabsInFolder_EveryTabInFolder_LeavesNoActiveTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\Project\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\Project\b.achx"));

        tm.CloseTabsInFolder(Folder);

        Assert.Empty(tm.Tabs);
        Assert.Null(tm.ActiveTab);
    }

    [Fact]
    public void CloseTabsInFolder_NothingInFolder_RaisesNoTabsChanged()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Elsewhere\outside.achx"));
        int tabsChanged = 0;
        tm.TabsChanged += () => tabsChanged++;

        var closed = tm.CloseTabsInFolder(Folder);

        Assert.Empty(closed);
        Assert.Equal(0, tabsChanged);
    }
}
