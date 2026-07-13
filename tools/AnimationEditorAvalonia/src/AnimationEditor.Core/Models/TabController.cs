using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Models
{
    /// <summary>
    /// Host-agnostic orchestration of the Animation Editor's open tabs, shared by the desktop
    /// and browser hosts. Currently owns the "leaving tab" capture couplet; broader
    /// tab-switch/close sequencing migrates here incrementally (issue #714).
    /// </summary>
    public sealed class TabController
    {
        private readonly IUndoManager _undoManager;
        private readonly IAppCommands _appCommands;
        private readonly Func<Dictionary<object, bool>> _captureTreeExpandState;

        /// <param name="captureTreeExpandState">
        /// Host callback returning the live tree's current expand state. Kept as a callback
        /// because the two hosts render the tree with different controls (desktop reads its
        /// <c>_treeRoots</c>; browser reads its <c>AnimationTreeControl</c>).
        /// </param>
        public TabController(
            IUndoManager undoManager,
            IAppCommands appCommands,
            Func<Dictionary<object, bool>> captureTreeExpandState)
        {
            _undoManager = undoManager;
            _appCommands = appCommands;
            _captureTreeExpandState = captureTreeExpandState;
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
    }
}
