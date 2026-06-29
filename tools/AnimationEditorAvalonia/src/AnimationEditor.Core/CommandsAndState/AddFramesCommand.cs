using FlatRedBall2.Animation.Content;
using System.Linq;

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
        private readonly List<object> _preSelection;
        private readonly int? _insertIndex;

        public string Description { get; }

        /// <param name="insertIndex">Where to place the frames. <c>null</c> appends at the
        /// end (Add Multiple Frames, or pasting into a chain with no frame selected); a value
        /// inserts the frames there in order (paste after the selected frame, matching
        /// Duplicate). Out-of-range values fall back to appending.</param>
        public AddFramesCommand(
            AnimationFrameSave[] frames, AnimationChainSave chain,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState,
            int? insertIndex = null)
        {
            _frames = frames;
            _chain = chain;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _insertIndex = insertIndex;
            _preSelection = new List<object>(selectedState.SelectedNodes);
            Description = frames.Length == 1
                ? $"Add Frame to '{chain.Name}'"
                : $"Add {frames.Length} Frames to '{chain.Name}'";
        }

        public bool Do()
        {
            if (_frames.Length == 0) return false;
            if (_insertIndex is int start && start >= 0 && start <= _chain.Frames.Count)
            {
                for (int i = 0; i < _frames.Length; i++)
                    _chain.Frames.Insert(start + i, _frames[i]);
            }
            else
            {
                foreach (var frame in _frames)
                    _chain.Frames.Add(frame);
            }
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedNodes = _frames.Cast<object>().ToList();
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
            _selectedState.SelectedNodes = _preSelection;
            _selectedState.SelectedFrame = _preSelection.OfType<AnimationFrameSave>().LastOrDefault();
        }
    }
}
