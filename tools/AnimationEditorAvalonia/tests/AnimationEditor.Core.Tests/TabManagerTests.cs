using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="TabManager"/> — open, focus, close, dedup, and restore logic.
/// </summary>
public class TabManagerTests
{
    private static FilePath P(string path) => new FilePath(path);

    // ── OpenOrFocus ───────────────────────────────────────────────────────────

    [Fact]
    public void OpenOrFocus_FirstFile_AddsTab()
    {
        var tm = new TabManager();

        var result = tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        Assert.Equal(TabOpenResult.Opened, result);
        Assert.Single(tm.Tabs);
    }

    [Fact]
    public void OpenOrFocus_FirstFile_SetsActiveTab()
    {
        var tm = new TabManager();

        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        Assert.NotNull(tm.ActiveTab);
        Assert.Equal(P(@"C:\Games\hero.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void OpenOrFocus_SecondFile_AddsBothTabs()
    {
        var tm = new TabManager();

        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));
        tm.OpenOrFocus(P(@"C:\Games\enemy.achx"));

        Assert.Equal(2, tm.Tabs.Count);
    }

    [Fact]
    public void OpenOrFocus_SecondFile_SetsSecondAsActive()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        tm.OpenOrFocus(P(@"C:\Games\enemy.achx"));

        Assert.Equal(P(@"C:\Games\enemy.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void OpenOrFocus_DuplicatePath_ReturnsFocused()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));
        tm.OpenOrFocus(P(@"C:\Games\enemy.achx"));

        var result = tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        Assert.Equal(TabOpenResult.Focused, result);
    }

    [Fact]
    public void OpenOrFocus_DuplicatePath_DoesNotAddNewTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        Assert.Single(tm.Tabs);
    }

    [Fact]
    public void OpenOrFocus_DuplicatePath_ActivatesExistingTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));
        tm.OpenOrFocus(P(@"C:\Games\enemy.achx"));

        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        Assert.Equal(P(@"C:\Games\hero.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void OpenOrFocus_DuplicatePathDifferentCase_Deduplicates()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\Hero.achx"));

        var result = tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        Assert.Equal(TabOpenResult.Focused, result);
        Assert.Single(tm.Tabs);
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Close_OnlyTab_ClearsAll()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));

        tm.Close(P(@"C:\Games\hero.achx"));

        Assert.Empty(tm.Tabs);
        Assert.Null(tm.ActiveTab);
    }

    [Fact]
    public void Close_ActiveTabWithNext_ActivatesNextTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\b.achx"));
        tm.OpenOrFocus(P(@"C:\Games\c.achx"));
        tm.Activate(P(@"C:\Games\b.achx"));

        tm.Close(P(@"C:\Games\b.achx"));

        Assert.Equal(P(@"C:\Games\c.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void Close_ActiveLastTab_ActivatesPreviousTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\b.achx"));

        tm.Close(P(@"C:\Games\b.achx"));

        Assert.Equal(P(@"C:\Games\a.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void Close_NonActiveTab_DoesNotChangeActiveTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\b.achx"));
        // b is active
        tm.Close(P(@"C:\Games\a.achx"));

        Assert.Equal(P(@"C:\Games\b.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void Close_UnknownPath_IsNoOp()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\hero.achx"));
        int before = tm.Tabs.Count;

        tm.Close(P(@"C:\Games\unknown.achx"));

        Assert.Equal(before, tm.Tabs.Count);
    }

    // ── Activate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_KnownPath_SetsActiveTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\b.achx"));

        tm.Activate(P(@"C:\Games\a.achx"));

        Assert.Equal(P(@"C:\Games\a.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void Activate_UnknownPath_IsNoOp()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));

        tm.Activate(P(@"C:\Games\unknown.achx"));

        Assert.Equal(P(@"C:\Games\a.achx"), tm.ActiveTab!.Path);
    }

    // ── RestoreFrom ───────────────────────────────────────────────────────────

    [Fact]
    public void RestoreFrom_EmptyList_TabsStayEmpty()
    {
        var tm = new TabManager();

        tm.RestoreFrom(new List<string>(), activePath: null);

        Assert.Empty(tm.Tabs);
        Assert.Null(tm.ActiveTab);
    }

    [Fact]
    public void RestoreFrom_ListWithPaths_CreatesTabs()
    {
        var tm = new TabManager();

        tm.RestoreFrom(
            new List<string> { @"C:\Games\a.achx", @"C:\Games\b.achx" },
            activePath: null);

        Assert.Equal(2, tm.Tabs.Count);
    }

    [Fact]
    public void RestoreFrom_WithActivePath_SetsCorrectActiveTab()
    {
        var tm = new TabManager();

        tm.RestoreFrom(
            new List<string> { @"C:\Games\a.achx", @"C:\Games\b.achx" },
            activePath: @"C:\Games\b.achx");

        Assert.Equal(P(@"C:\Games\b.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void RestoreFrom_ActivePathNotInList_FirstTabBecomesActive()
    {
        var tm = new TabManager();

        tm.RestoreFrom(
            new List<string> { @"C:\Games\a.achx" },
            activePath: @"C:\Games\gone.achx");

        Assert.Equal(P(@"C:\Games\a.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void RestoreFrom_ClearsExistingTabs()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\old.achx"));

        tm.RestoreFrom(new List<string> { @"C:\Games\new.achx" }, activePath: null);

        Assert.Single(tm.Tabs);
        Assert.Equal(P(@"C:\Games\new.achx"), tm.Tabs[0].Path);
    }

    // ── OpenTabPaths helper ───────────────────────────────────────────────────

    [Fact]
    public void OpenTabPaths_ReturnsFullPathsForAllTabs()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\b.achx"));

        var paths = tm.OpenTabPaths;

        // FilePath normalises separators to '/'; compare via FilePath equality.
        Assert.Equal(2, paths.Count);
        Assert.Equal(P(@"C:\Games\a.achx"), new FilePath(paths[0]));
        Assert.Equal(P(@"C:\Games\b.achx"), new FilePath(paths[1]));
    }

    // ── ActiveChanged event ───────────────────────────────────────────────────

    [Fact]
    public void OpenOrFocus_FirstFile_RaisesActiveChanged()
    {
        var tm = new TabManager();
        bool raised = false;
        tm.ActiveChanged += _ => raised = true;

        tm.OpenOrFocus(P(@"C:\Games\a.achx"));

        Assert.True(raised);
    }

    [Fact]
    public void Close_RaisesActiveChanged()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        bool raised = false;
        tm.ActiveChanged += _ => raised = true;

        tm.Close(P(@"C:\Games\a.achx"));

        Assert.True(raised);
    }
}
