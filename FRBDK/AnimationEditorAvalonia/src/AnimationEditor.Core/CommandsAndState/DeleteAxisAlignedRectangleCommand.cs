using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteAxisAlignedRectangleCommand : IUndoableCommand
    {
        private readonly AARectSave _rect;
        private readonly AnimationFrameSave _frame;
        private readonly int _originalIndex;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public DeleteAxisAlignedRectangleCommand(
            AARectSave rect,
            AnimationFrameSave frame,
            int originalIndex,
            IAppCommands commands,
            IApplicationEvents events)
        {
            _rect = rect;
            _frame = frame;
            _originalIndex = originalIndex;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            int idx = Math.Min(_originalIndex, _frame.ShapesSave!.AARectSaves.Count);
            _frame.ShapesSave!.AARectSaves.Insert(idx, _rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapesSave!.AARectSaves.Remove(_rect);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
