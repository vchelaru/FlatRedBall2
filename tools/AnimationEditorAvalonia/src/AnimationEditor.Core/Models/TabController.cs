using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Paths;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// Whether <see cref="TabController.EnsureCurrentDocumentHasTab"/> should make the new/found
    /// tab active or merely register it. Use <see cref="Background"/> when switching away to a
    /// different tab that is about to become active (the outgoing tab must not steal focus);
    /// use <see cref="Activate"/> when nothing else is being switched to (e.g. Add Animation on
    /// a document with zero open tabs, #898).
    /// </summary>
    public enum TabActivation
    {
        Background,
        Activate,
    }

    /// <summary>
    /// Host-agnostic orchestration of the AnimationEditor's open tabs, shared by the desktop
    /// and browser hosts. Owns the "leaving tab" capture couplet and "ensure a tab exists for
    /// the current document" logic; broader tab-switch/close sequencing migrates here
    /// incrementally (issue #714).
    /// </summary>
    public sealed class TabController
    {
        private readonly IUndoManager _undoManager;
        private readonly IAppCommands _appCommands;
        private readonly Func<Dictionary<object, bool>> _captureTreeExpandState;
        private readonly TabManager _tabManager;

        /// <param name="captureTreeExpandState">
        /// Host callback returning the live tree's current expand state. Kept as a callback
        /// because the two hosts render the tree with different controls (desktop reads its
        /// <c>_treeRoots</c>; browser reads its <c>AnimationTreeControl</c>).
        /// </param>
        public TabController(
            IUndoManager undoManager,
            IAppCommands appCommands,
            Func<Dictionary<object, bool>> captureTreeExpandState,
            TabManager tabManager)
        {
            _undoManager = undoManager;
            _appCommands = appCommands;
            _captureTreeExpandState = captureTreeExpandState;
            _tabManager = tabManager;
        }

        /// <summary>
        /// Snapshots everything the tab being deactivated needs to restore later — its undo
        /// history, in-memory editor model + selection, and tree expand state (including frame
        /// nodes with shape children, which have no other persistence, #687). Call at every
        /// "leaving tab" site so no site can silently drop one of the three and reintroduce #687.
        /// </summary>
        public void CaptureLeavingTab(TabEntry leaving)
        {
            leaving.UndoSnapshot = _undoManager.TakeSnapshot();
            _appCommands.CaptureTabEditorState(leaving);
            leaving.CachedTreeExpandState = _captureTreeExpandState();
        }

        /// <summary>
        /// Ensures the currently-loaded document — on disk or unsaved — has a tab. No-op when
        /// the document is unsaved and empty (nothing worth a tab yet), or when it already has
        /// a tracked tab. Built from the same primitives <c>OpenAsNewUnsavedDocument</c> and the
        /// old per-site <c>EnsureCurrentEditorContentHasTab</c> duplicated (#898): a project with
        /// zero open tabs — e.g. after Open Project Folder on a folder with no files, or after
        /// closing the last tab — otherwise leaves any content added next (like Add Animation)
        /// with no visible tab.
        /// </summary>
        public void EnsureCurrentDocumentHasTab(IProjectManager projectManager, TabActivation activation)
        {
            var currentPath = projectManager.FileName;
            if (!string.IsNullOrEmpty(currentPath))
            {
                var fp = new FilePath(currentPath);
                if (_tabManager.Tabs.All(t => t.Path != fp))
                {
                    if (activation == TabActivation.Activate)
                        _tabManager.OpenOrFocus(fp);
                    else
                        _tabManager.RegisterBackground(fp);
                }
                return;
            }

            if (_tabManager.Tabs.Count != 0 ||
                projectManager.AnimationChainListSave is not { AnimationChains.Count: > 0 })
                return;

            var displayName = TabManager.ComputeUntitledDisplayName(
                _tabManager.Tabs.Select(t => t.DisplayName).ToList());
            var sentinelPath = new FilePath(_tabManager.NewUntitledSentinelPath());
            if (activation == TabActivation.Activate)
                _tabManager.OpenOrFocus(sentinelPath, displayName);
            else
                _tabManager.RegisterBackground(sentinelPath, displayName);
        }
    }
}
