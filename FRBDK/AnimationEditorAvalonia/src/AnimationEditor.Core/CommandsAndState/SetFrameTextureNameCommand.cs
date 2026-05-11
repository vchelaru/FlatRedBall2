using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class SetFrameTextureNameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly string? _oldName;
        private readonly string? _newName;

        public SetFrameTextureNameCommand(AnimationFrameSave frame, string? oldName, string? newName)
        {
            _frame = frame;
            _oldName = oldName;
            _newName = newName;
        }

        public void Undo()
        {
            _frame.TextureName = _oldName!;
            AppCommands.Self.RefreshTreeNode(_frame);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
        }

        public void Redo()
        {
            _frame.TextureName = _newName!;
            AppCommands.Self.RefreshTreeNode(_frame);
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
        }
    }
}
