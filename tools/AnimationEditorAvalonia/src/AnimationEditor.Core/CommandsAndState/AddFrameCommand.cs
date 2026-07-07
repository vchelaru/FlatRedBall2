using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddFrameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly AnimationChainSave _chain;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;
        private readonly ISelectedState _selectedState;
        private readonly AnimationFrameSave? _preAddFrame;

        public string Description { get; }

        public AddFrameCommand(AnimationFrameSave frame, AnimationChainSave chain,
            IAppCommands commands, IApplicationEvents events, ISelectedState selectedState)
        {
            _frame = frame;
            _chain = chain;
            _commands = commands;
            _events = events;
            _selectedState = selectedState;
            _preAddFrame = selectedState.SelectedFrame;
            Description = $"Add Frame to '{chain.Name}'";
        }

        public bool Do()
        {
            _chain.Frames.Add(_frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedFrame = _frame;
            // Arm the wireframe to fit this frame into view on the reload the selection above triggers,
            // in case it's larger than the viewport (e.g. a whole-sheet default) — #616. Undo does not
            // re-arm: restoring the prior selection shouldn't surprise-zoom the user.
            _events.RaiseFitFrameToViewRequested();
            return true;
        }

        public void Undo()
        {
            _chain.Frames.Remove(_frame);
            _commands.RefreshTreeNode(_chain);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            _selectedState.SelectedFrame = _preAddFrame;
        }
    }
}
