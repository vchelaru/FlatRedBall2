using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>Renames a chain. Also marks it (via <see
    /// cref="IProjectManager.MarkChainNameExplicit"/>) as having a real name from now on, in a
    /// native-tsx project, so <see cref="IProjectManager.TrySetTsxOwnerTileId"/> never overwrites it
    /// again -- and snapshots/restores the whole tsx state around that (same as <see
    /// cref="SetChainTsxOwnerTileIdCommand"/>) so Undo puts the chain back in the synthetic-name set
    /// if it started there. A <see langword="null"/> new name reverts a native-tsx chain to its
    /// synthetic auto name instead (<see cref="IProjectManager.MakeChainNameAuto"/>).</summary>
    internal sealed class RenameChainCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly string _oldName;
        private readonly string? _newName;
        private readonly IProjectManager _pm;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private object? _before;

        public string Description { get; }

        public RenameChainCommand(AnimationChainSave chain, string oldName, string? newName,
            IProjectManager pm, IAppCommands commands, IApplicationEvents events)
        {
            _chain = chain;
            _oldName = oldName;
            _newName = newName;
            _pm = pm;
            _commands = commands;
            _events = events;
            Description = newName is null ? $"Clear name '{oldName}'" : $"Rename '{oldName}' → '{newName}'";
        }

        public bool Do()
        {
            _before = _pm.CaptureTsxState();
            if (_newName is null)
            {
                if (!_pm.MakeChainNameAuto(_chain))
                    return false;
            }
            else
            {
                _chain.Name = _newName;
                _pm.MarkChainNameExplicit(_chain);
            }
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            return true;
        }

        public void Undo()
        {
            _chain.Name = _oldName;
            _pm.RestoreTsxState(_before);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
        }
    }
}
