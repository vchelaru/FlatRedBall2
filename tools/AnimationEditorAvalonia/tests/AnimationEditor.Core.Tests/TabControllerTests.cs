using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Verifies the shared "leaving tab" capture couplet lives in one place so neither host can
/// drop one of the three captured pieces and reintroduce #687 (lost tree expand state).
/// </summary>
public class TabControllerTests
{
    private sealed class StubCommand : IUndoableCommand
    {
        public string Description => "Stub";
        public bool Do() => true;
        public void Undo() { }
    }

    [Fact]
    public void CaptureLeavingTab_CapturesUndoEditorAndTreeExpandState()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "Idle");
        // A recorded command makes the undo snapshot non-trivial, proving it was captured.
        ctx.UndoManager.Execute(new StubCommand());

        var expandState = new Dictionary<object, bool> { [new object()] = true };
        var controller = new TabController(ctx.UndoManager, ctx.AppCommands, () => expandState, new TabManager());
        var tab = new TabEntry(new FilePath(""));

        controller.CaptureLeavingTab(tab);

        Assert.Single(tab.UndoSnapshot!.UndoStack);           // undo history captured
        Assert.NotNull(tab.CachedEditorModel);                // in-memory editor model captured
        Assert.Same(expandState, tab.CachedTreeExpandState);  // #687 tree expand state captured
    }

    // ── EnsureCurrentDocumentHasTab (#898) ──────────────────────────────────────
    // Add Animation (and any other "start editing with zero tabs open" site) needs the new
    // tab to become the active one.

    private static TabController MakeController(TestServices ctx, TabManager tabManager) =>
        new(ctx.UndoManager, ctx.AppCommands, () => new Dictionary<object, bool>(), tabManager);

    [Fact]
    public void EnsureCurrentDocumentHasTab_ZeroTabsUnsavedContentActivate_OpensAndActivatesTab()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "Idle");
        var tabManager = new TabManager();
        var controller = MakeController(ctx, tabManager);

        controller.EnsureCurrentDocumentHasTab(ctx.ProjectManager);

        Assert.Single(tabManager.Tabs);
        Assert.NotNull(tabManager.ActiveTab);
        Assert.Equal("Untitled", tabManager.ActiveTab!.DisplayName);
    }

    [Fact]
    public void EnsureCurrentDocumentHasTab_NoChainsYet_NoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls(); // empty ACLS, no chains
        var tabManager = new TabManager();
        var controller = MakeController(ctx, tabManager);

        controller.EnsureCurrentDocumentHasTab(ctx.ProjectManager);

        Assert.Empty(tabManager.Tabs);
    }

    [Fact]
    public void EnsureCurrentDocumentHasTab_TabsAlreadyOpen_NoOp()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        TestHelpers.MakeChain(ctx.Acls, "Idle");
        var tabManager = new TabManager();
        tabManager.OpenOrFocus(new FilePath(tabManager.NewUntitledSentinelPath()), "Untitled");
        var controller = MakeController(ctx, tabManager);

        controller.EnsureCurrentDocumentHasTab(ctx.ProjectManager);

        Assert.Single(tabManager.Tabs); // no second tab opened
    }

    [Fact]
    public void EnsureCurrentDocumentHasTab_SavedFileActivate_OpensAndActivatesItsTab()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = @"C:\Games\hero.achx";
        var tabManager = new TabManager();
        var controller = MakeController(ctx, tabManager);

        controller.EnsureCurrentDocumentHasTab(ctx.ProjectManager);

        Assert.Single(tabManager.Tabs);
        Assert.Equal(new FilePath(@"C:\Games\hero.achx"), tabManager.ActiveTab!.Path);
    }

    [Fact]
    public void EnsureCurrentDocumentHasTab_SavedFileAlreadyTracked_NoDuplicate()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        ctx.ProjectManager.FileName = @"C:\Games\hero.achx";
        var tabManager = new TabManager();
        tabManager.OpenOrFocus(new FilePath(@"C:\Games\hero.achx"));
        var controller = MakeController(ctx, tabManager);

        controller.EnsureCurrentDocumentHasTab(ctx.ProjectManager);

        Assert.Single(tabManager.Tabs);
    }
}
