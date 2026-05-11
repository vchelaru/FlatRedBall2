using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using System;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class DeleteCircleCommand : IUndoableCommand
    {
        private readonly CircleSave _circle;
        private readonly AnimationFrameSave _frame;
        private readonly int _originalIndex;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public DeleteCircleCommand(CircleSave circle, AnimationFrameSave frame, int originalIndex,
            IAppCommands commands, IApplicationEvents events)
        {
            _circle = circle;
            _frame = frame;
            _originalIndex = originalIndex;
            _commands = commands;
            _events = events;
        }

        public void Undo()
        {
            int idx = Math.Min(_originalIndex, _frame.ShapeCollectionSave.CircleSaves.Count);
            _frame.ShapeCollectionSave.CircleSaves.Insert(idx, _circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            _frame.ShapeCollectionSave.CircleSaves.Remove(_circle);
            _commands.RefreshTreeNode(_frame);
            _commands.RefreshAnimationFrameDisplay();
            _events.RaiseAnimationChainsChanged();
            _commands.SaveCurrentAnimationChainList();
        }
    }
}
