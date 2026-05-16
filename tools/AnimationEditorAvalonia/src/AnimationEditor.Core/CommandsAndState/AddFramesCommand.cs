using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// Undo/redo record for adding several frames to a chain in one operation
    /// (Add Multiple Frames). Recorded as a single entry so one user action is
    /// one undo step, rather than one step per frame.
    /// </summary>
    internal sealed class AddFramesCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave[] _frames;
        private readonly AnimationChainSave _chain;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;
        private readonly AnimationFrameSave? _preAddFrame;

        public string Description { get; }

        public AddFramesCommand(
            AnimationFrameSave[] frames, AnimationChainSave chain,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _frames = frames;
            _chain = chain;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _preAddFrame = selectedState.SelectedFrame;
            Description = frames.Length == 1
                ? $"Add Frame to '{chain.Name}'"
                : $"Add {frames.Length} Frames to '{chain.Name}'";
        }

        public bool Do()
        {
            if (_frames.Length == 0) return false;
            foreach (var frame in _frames)
                _chain.Frames.Add(frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedFrame = _frames[^1];
            return true;
        }

        public void Undo()
        {
            foreach (var frame in _frames)
                _chain.Frames.Remove(frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedFrame = _preAddFrame;
        }
    }
}
