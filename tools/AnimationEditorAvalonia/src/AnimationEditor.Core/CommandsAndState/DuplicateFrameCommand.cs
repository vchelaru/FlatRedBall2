using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Undo/redo record for duplicating a single animation frame.
    /// The copy is inserted immediately after the source frame so it appears
    /// adjacent to the original in the tree.
    /// </summary>
    internal sealed class DuplicateFrameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _source;
        private readonly AnimationFrameSave _copy;
        private readonly AnimationChainSave _chain;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;
        private readonly int _insertIndex;

        public string Description { get; }

        public DuplicateFrameCommand(
            AnimationFrameSave source,
            AnimationFrameSave copy,
            AnimationChainSave chain,
            IAppCommands commands,
            IApplicationEvents events,
            ISelectedState selectedState)
        {
            _source = source;
            _copy = copy;
            _chain = chain;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _insertIndex = chain.Frames.IndexOf(source) + 1;
            Description = $"Duplicate Frame in '{chain.Name}'";
        }

        public bool Do()
        {
            int idx = Math.Min(_insertIndex, _chain.Frames.Count);
            _chain.Frames.Insert(idx, _copy);
            RaiseSideEffects();
            _selectedState.SelectedFrame = _copy;
            return true;
        }

        public void Undo()
        {
            _chain.Frames.Remove(_copy);
            RaiseSideEffects();
            _selectedState.SelectedFrame = _source;
        }

        public void Redo()
        {
            int idx = Math.Min(_insertIndex, _chain.Frames.Count);
            _chain.Frames.Insert(idx, _copy);
            RaiseSideEffects();
            _selectedState.SelectedFrame = _copy;
        }

        private void RaiseSideEffects()
        {
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
