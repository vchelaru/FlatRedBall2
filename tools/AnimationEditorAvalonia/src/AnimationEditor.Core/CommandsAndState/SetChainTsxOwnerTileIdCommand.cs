using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Explicitly overrides a native-tsx chain's owner tile (issue #1182) via <see
    /// cref="IProjectManager.TrySetTsxOwnerTileId"/>, as one undo step. Undo/Redo round-trip the
    /// whole tsx tracking snapshot (<see cref="IProjectManager.CaptureTsxState"/>/<see
    /// cref="IProjectManager.RestoreTsxState"/>) rather than just the one dictionary entry: the
    /// "before" state might have had a tracked origin frame or satellite hints that <see
    /// cref="IProjectManager.TrySetTsxOwnerTileId"/> deliberately drops on an explicit set, and
    /// only the full-state capture/restore already used elsewhere for tab-switching knows how to
    /// put those back.
    /// </summary>
    internal sealed class SetChainTsxOwnerTileIdCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly uint _tileId;
        private readonly IProjectManager _pm;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private object? _before;

        public string Description { get; }

        /// <summary>Validation error from the most recent <see cref="Do"/>/<see cref="Redo"/>, or
        /// <see langword="null"/> after a successful one. A caller reads this right after calling
        /// <see cref="IUndoManager.Execute"/> to surface the reason a set was rejected.</summary>
        public string? Error { get; private set; }

        public SetChainTsxOwnerTileIdCommand(
            AnimationChainSave chain, uint tileId, IProjectManager pm, IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _tileId = tileId;
            _pm = pm;
            _commands = commands;
            _events = events;
            Description = $"Set Owner Tile for '{chain.Name}'";
        }

        public bool Do()
        {
            // Already exactly this value with nothing left to drop -- returning false here (like
            // the error path below) keeps UndoManager.Execute from pushing a no-op undo entry for
            // a repeated "Sync to First Frame" click (#1182 follow-up).
            if (_pm.IsTsxOwnerTileIdAlreadySet(_chain, _tileId))
            {
                Error = null;
                return false;
            }

            _before = _pm.CaptureTsxState();
            Error = _pm.TrySetTsxOwnerTileId(_chain, _tileId);
            if (Error != null)
                return false;

            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            return true;
        }

        public void Undo()
        {
            _pm.RestoreTsxState(_before);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
        }
    }
}
