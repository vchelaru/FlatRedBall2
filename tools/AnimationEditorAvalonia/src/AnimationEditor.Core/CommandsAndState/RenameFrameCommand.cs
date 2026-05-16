using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class RenameFrameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly string _oldName;
        private readonly bool _oldHasCustomName;
        private readonly string _newName;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description { get; }

        public RenameFrameCommand(AnimationFrameSave frame, string newName,
            IAppCommands commands, IApplicationEvents events)
        {
            _frame = frame;
            _oldName = frame.Name;
            _oldHasCustomName = frame.HasCustomName;
            _newName = newName;
            _commands = commands;
            _events = events;
            Description = $"Rename Frame → '{newName}'";
        }

        public bool Do()
        {
            _frame.Name = _newName;
            _frame.HasCustomName = true;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
            return true;
        }

        public void Undo()
        {
            _frame.Name = _oldName;
            _frame.HasCustomName = _oldHasCustomName;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
