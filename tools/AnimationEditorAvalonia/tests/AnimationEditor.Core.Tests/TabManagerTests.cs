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
    public void OpenOrFocus_WithDisplayName_SetsDisplayName()
    {
        var tm = new TabManager();

        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");

        Assert.Equal("Untitled", tm.ActiveTab!.DisplayName);
    }

    [Fact]
    public void OpenOrFocus_WithDisplayName_DuplicatePath_ReturnsFocused()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");

        var result = tm.OpenOrFocus(P("__untitled__:1"), "Untitled");

        Assert.Equal(TabOpenResult.Focused, result);
        Assert.Single(tm.Tabs);
    }

    [Fact]
    public void OpenOrFocus_TwoUntitledSentinels_CreatesTwoTabs()
    {
        var tm = new TabManager();

        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");
        tm.OpenOrFocus(P("__untitled__:2"), "Untitled (1)");

        Assert.Equal(2, tm.Tabs.Count);
    }

    // ── ComputeUntitledDisplayName ─────────────────────────────────────────────

    [Fact]
    public void ComputeUntitledDisplayName_NoExisting_ReturnsUntitled()
    {
        var result = TabManager.ComputeUntitledDisplayName(new List<string>());

        Assert.Equal("Untitled", result);
    }

    [Fact]
    public void ComputeUntitledDisplayName_UntitledTaken_ReturnsUntitled1()
    {
        var result = TabManager.ComputeUntitledDisplayName(new List<string> { "Untitled" });

        Assert.Equal("Untitled (1)", result);
    }

    [Fact]
    public void ComputeUntitledDisplayName_UntitledAnd1Taken_ReturnsUntitled2()
    {
        var names = new List<string> { "Untitled", "Untitled (1)" };

        var result = TabManager.ComputeUntitledDisplayName(names);

        Assert.Equal("Untitled (2)", result);
    }

    [Fact]
    public void ComputeUntitledDisplayName_GapInSequence_SkipsGap()
    {
        // "Untitled (1)" is taken but "Untitled (2)" is not — but first gap after base is (1).
        var names = new List<string> { "Untitled", "Untitled (2)" };

        var result = TabManager.ComputeUntitledDisplayName(names);

        Assert.Equal("Untitled (1)", result);
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

    // ── TabsChanged event (issue #439) ────────────────────────────────────────

    [Fact]
    public void OpenOrFocus_RaisesTabsChanged()
    {
        var tm = new TabManager();
        int count = 0;
        tm.TabsChanged += () => count++;

        tm.OpenOrFocus(P(@"C:\Games\a.achx"));

        Assert.Equal(1, count);
    }

    [Fact]
    public void Activate_RaisesTabsChanged()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\b.achx"));
        bool raised = false;
        tm.TabsChanged += () => raised = true;

        tm.Activate(P(@"C:\Games\a.achx"));

        Assert.True(raised);
    }

    [Fact]
    public void Close_ActiveTab_RaisesTabsChanged()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        bool raised = false;
        tm.TabsChanged += () => raised = true;

        tm.Close(P(@"C:\Games\a.achx"));

        Assert.True(raised);
    }

    [Fact]
    public void Close_BackgroundTab_RaisesTabsChanged_EvenThoughActiveChangedDoesNot()
    {
        // The core of issue #439: closing a non-active tab changes the open-tab set but
        // leaves ActiveTab untouched, so ActiveChanged stays silent. TabsChanged must fire
        // so the session is re-persisted.
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\a.achx"));
        tm.OpenOrFocus(P(@"C:\Games\b.achx")); // b is active
        bool activeChanged = false, tabsChanged = false;
        tm.ActiveChanged += _ => activeChanged = true;
        tm.TabsChanged += () => tabsChanged = true;

        tm.Close(P(@"C:\Games\a.achx")); // close the background tab

        Assert.False(activeChanged);
        Assert.True(tabsChanged);
    }

    [Fact]
    public void Move_RaisesTabsChanged()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        bool raised = false;
        tm.TabsChanged += () => raised = true;

        tm.Move(P(@"C:\a.achx"), 1);

        Assert.True(raised);
    }

    [Fact]
    public void Move_SameIndex_DoesNotRaiseTabsChanged()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        bool raised = false;
        tm.TabsChanged += () => raised = true;

        tm.Move(P(@"C:\a.achx"), 0); // already at index 0 — no change

        Assert.False(raised);
    }

    [Fact]
    public void RegisterBackground_RaisesTabsChanged()
    {
        var tm = new TabManager();
        bool raised = false;
        tm.TabsChanged += () => raised = true;

        tm.RegisterBackground(P(@"C:\Games\existing.achx"));

        Assert.True(raised);
    }

    [Fact]
    public void Rename_RaisesTabsChanged()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");
        bool raised = false;
        tm.TabsChanged += () => raised = true;

        tm.Rename(P("__untitled__:1"), P(@"C:\hero.achx"));

        Assert.True(raised);
    }

    [Fact]
    public void RestoreFrom_RaisesTabsChanged()
    {
        var tm = new TabManager();
        bool raised = false;
        tm.TabsChanged += () => raised = true;

        tm.RestoreFrom(new List<string> { @"C:\a.achx" }, activePath: null);

        Assert.True(raised);
    }

    // ── RegisterBackground ────────────────────────────────────────────────────

    [Fact]
    public void RegisterBackground_AddsTabAtPositionZero()
    {
        var tm = new TabManager();

        tm.RegisterBackground(P(@"C:\Games\existing.achx"));

        Assert.Single(tm.Tabs);
        Assert.Equal(P(@"C:\Games\existing.achx"), tm.Tabs[0].Path);
    }

    [Fact]
    public void RegisterBackground_DoesNotActivateTab()
    {
        var tm = new TabManager();

        tm.RegisterBackground(P(@"C:\Games\existing.achx"));

        Assert.Null(tm.ActiveTab);
    }

    [Fact]
    public void RegisterBackground_DoesNotRaiseActiveChanged()
    {
        var tm = new TabManager();
        bool raised = false;
        tm.ActiveChanged += _ => raised = true;

        tm.RegisterBackground(P(@"C:\Games\existing.achx"));

        Assert.False(raised);
    }

    [Fact]
    public void RegisterBackground_IsNoOpIfPathAlreadyPresent()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\existing.achx"));

        tm.RegisterBackground(P(@"C:\Games\existing.achx"));

        Assert.Single(tm.Tabs);
    }

    [Fact]
    public void RegisterBackground_InsertsBeforeExistingTabs()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\Games\second.achx"));

        tm.RegisterBackground(P(@"C:\Games\first.achx"));

        Assert.Equal(2, tm.Tabs.Count);
        Assert.Equal(P(@"C:\Games\first.achx"), tm.Tabs[0].Path);
        Assert.Equal(P(@"C:\Games\second.achx"), tm.Tabs[1].Path);
    }

    [Fact]
    public void RegisterBackground_AfterRegister_OpenOrFocusActivatesNewFileButKeepsBoth()
    {
        var tm = new TabManager();
        tm.RegisterBackground(P(@"C:\Games\existing.achx"));

        tm.OpenOrFocus(P(@"C:\Games\new.achx"));

        Assert.Equal(2, tm.Tabs.Count);
        Assert.Equal(P(@"C:\Games\existing.achx"), tm.Tabs[0].Path);
        Assert.Equal(P(@"C:\Games\new.achx"), tm.Tabs[1].Path);
        Assert.Equal(P(@"C:\Games\new.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void RegisterBackground_WithDisplayNameOverride_UsesOverrideInDisplayName()
    {
        var tm = new TabManager();

        tm.RegisterBackground(P(@"C:\Games\existing.achx"), "My Custom Label");

        Assert.Single(tm.Tabs);
        Assert.Equal("My Custom Label", tm.Tabs[0].DisplayName);
    }

    // ── TabEntry.Kind (issue #604) ────────────────────────────────────────────

    [Fact]
    public void TabEntry_Kind_AchxPath_IsAchx()
    {
        var entry = new TabEntry(P(@"C:\Games\hero.achx"));

        Assert.Equal(TabKind.Achx, entry.Kind);
    }

    [Fact]
    public void TabEntry_Kind_PngPath_IsPng()
    {
        var entry = new TabEntry(P(@"C:\Games\sheet.png"));

        Assert.Equal(TabKind.Png, entry.Kind);
    }

    [Fact]
    public void TabEntry_Kind_UppercasePngExtension_IsPng()
    {
        var entry = new TabEntry(P(@"C:\Games\SHEET.PNG"));

        Assert.Equal(TabKind.Png, entry.Kind);
    }

    [Fact]
    public void TabEntry_Kind_UntitledSentinel_IsAchx()
    {
        // Untitled sentinels ("__untitled__:1") have no .png extension, so a new unsaved
        // file must never be mistaken for a PNG tab.
        var entry = new TabEntry(P("__untitled__:1"), "Untitled");

        Assert.Equal(TabKind.Achx, entry.Kind);
    }

    [Fact]
    public void RestoreFrom_PngPath_DerivesPngKind()
    {
        var tm = new TabManager();

        tm.RestoreFrom(new List<string> { @"C:\Games\sheet.png" }, activePath: null);

        Assert.Equal(TabKind.Png, tm.Tabs[0].Kind);
    }

    // ── TabEntry.DisplayName ──────────────────────────────────────────────────

    [Fact]
    public void TabEntry_DisplayName_UsesOverrideWhenProvided()
    {
        var entry = new TabEntry(P(@"C:\Games\foo.achx"), "My Override");

        Assert.Equal("My Override", entry.DisplayName);
    }

    [Fact]
    public void TabEntry_DisplayName_FallsBackToNoPathWhenNoOverride()
    {
        var entry = new TabEntry(P(@"C:\Games\foo.achx"));

        Assert.Equal("foo.achx", entry.DisplayName);
    }

    [Fact]
    public void TabEntry_DisplayName_ReturnsUntitledWhenPathOriginalIsNullOrEmpty()
    {
        // Construct via RegisterBackground with an empty-original FilePath sentinel.
        var entry = new TabEntry(new FilePath(null!));

        Assert.Equal("Untitled", entry.DisplayName);
    }

    // ── Move ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Move_FirstToLast_ReordersCorrectly()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        tm.OpenOrFocus(P(@"C:\c.achx"));

        tm.Move(P(@"C:\a.achx"), 2);

        Assert.Equal("a.achx", tm.Tabs[2].Path.NoPath);
        Assert.Equal("b.achx", tm.Tabs[0].Path.NoPath);
        Assert.Equal("c.achx", tm.Tabs[1].Path.NoPath);
    }

    [Fact]
    public void Move_LastToFirst_ReordersCorrectly()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        tm.OpenOrFocus(P(@"C:\c.achx"));

        tm.Move(P(@"C:\c.achx"), 0);

        Assert.Equal("c.achx", tm.Tabs[0].Path.NoPath);
        Assert.Equal("a.achx", tm.Tabs[1].Path.NoPath);
        Assert.Equal("b.achx", tm.Tabs[2].Path.NoPath);
    }

    [Fact]
    public void Move_MiddleToEnd_ReordersCorrectly()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        tm.OpenOrFocus(P(@"C:\c.achx"));

        tm.Move(P(@"C:\b.achx"), 2);

        Assert.Equal("a.achx", tm.Tabs[0].Path.NoPath);
        Assert.Equal("c.achx", tm.Tabs[1].Path.NoPath);
        Assert.Equal("b.achx", tm.Tabs[2].Path.NoPath);
    }

    [Fact]
    public void Move_SameIndex_IsNoOp()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));

        tm.Move(P(@"C:\a.achx"), 0);

        Assert.Equal("a.achx", tm.Tabs[0].Path.NoPath);
        Assert.Equal("b.achx", tm.Tabs[1].Path.NoPath);
    }

    [Fact]
    public void Move_NegativeIndex_ClampsToZero()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        tm.OpenOrFocus(P(@"C:\c.achx"));

        tm.Move(P(@"C:\c.achx"), -5);

        Assert.Equal("c.achx", tm.Tabs[0].Path.NoPath);
    }

    [Fact]
    public void Move_IndexBeyondEnd_ClampsToLast()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        tm.OpenOrFocus(P(@"C:\c.achx"));

        tm.Move(P(@"C:\a.achx"), 99);

        Assert.Equal("a.achx", tm.Tabs[2].Path.NoPath);
    }

    [Fact]
    public void Move_UnknownPath_IsNoOp()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));

        tm.Move(P(@"C:\nonexistent.achx"), 0); // must not throw

        Assert.Equal(2, tm.Tabs.Count);
        Assert.Equal("a.achx", tm.Tabs[0].Path.NoPath);
    }

    [Fact]
    public void Move_DoesNotChangeActiveTab()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P(@"C:\b.achx"));
        tm.OpenOrFocus(P(@"C:\c.achx")); // c is active

        tm.Move(P(@"C:\a.achx"), 2);

        Assert.Equal(P(@"C:\c.achx"), tm.ActiveTab!.Path);
    }

    // ── Rename ────────────────────────────────────────────────────────────────

    [Fact]
    public void Rename_UpdatesTabPath()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");

        tm.Rename(P("__untitled__:1"), P(@"C:\projects\hero.achx"));

        Assert.Equal(P(@"C:\projects\hero.achx"), tm.Tabs[0].Path);
    }

    [Fact]
    public void Rename_DisplayNameBecomesFilename()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");

        tm.Rename(P("__untitled__:1"), P(@"C:\projects\hero.achx"));

        Assert.Equal("hero.achx", tm.Tabs[0].DisplayName);
    }

    [Fact]
    public void Rename_PreservesTabPosition()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));
        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");
        tm.OpenOrFocus(P(@"C:\c.achx"));
        tm.Activate(P("__untitled__:1"));

        tm.Rename(P("__untitled__:1"), P(@"C:\b.achx"));

        Assert.Equal("a.achx", tm.Tabs[0].Path.NoPath);
        Assert.Equal("b.achx", tm.Tabs[1].Path.NoPath);
        Assert.Equal("c.achx", tm.Tabs[2].Path.NoPath);
    }

    [Fact]
    public void Rename_ActiveTabRemainsActive()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P("__untitled__:1"), "Untitled");

        tm.Rename(P("__untitled__:1"), P(@"C:\hero.achx"));

        Assert.Equal(P(@"C:\hero.achx"), tm.ActiveTab!.Path);
    }

    [Fact]
    public void Rename_UnknownPath_IsNoOp()
    {
        var tm = new TabManager();
        tm.OpenOrFocus(P(@"C:\a.achx"));

        tm.Rename(P(@"C:\nonexistent.achx"), P(@"C:\b.achx")); // must not throw

        Assert.Single(tm.Tabs);
        Assert.Equal("a.achx", tm.Tabs[0].Path.NoPath);
    }
}
