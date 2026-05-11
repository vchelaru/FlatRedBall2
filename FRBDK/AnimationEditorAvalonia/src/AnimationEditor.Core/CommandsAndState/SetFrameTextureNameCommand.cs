using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class SetFrameTextureNameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly string? _oldName;
        private readonly string? _newName;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public SetFrameTextureNameCommand(AnimationFrameSave frame, string? oldName, string? newName,
            IAppCommands commands, IApplicationEvents events)
        {
            _frame = frame;
            _oldName = oldName;
            _newName = newName;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            _frame.TextureName = _oldName!;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
        }

        public void Redo()
        {
            _frame.TextureName = _newName!;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
        }
    }
}
