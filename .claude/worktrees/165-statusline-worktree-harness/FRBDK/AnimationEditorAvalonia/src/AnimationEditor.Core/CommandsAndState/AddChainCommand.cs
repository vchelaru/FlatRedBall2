using FlatRedBall.Content.AnimationChain;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class AddChainCommand : IUndoableCommand
    {
        private readonly AnimationChainSave _chain;
        private readonly AnimationChainListSave _acls;
        private readonly int _insertedAtIndex;

        public AddChainCommand(AnimationChainSave chain, AnimationChainListSave acls, int insertedAtIndex)
        {
            _chain = chain;
            _acls = acls;
            _insertedAtIndex = insertedAtIndex;
        }

        public void Undo()
        {
            _acls.AnimationChains.Remove(_chain);
            AppCommands.Self.RefreshTreeView();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }

        public void Redo()
        {
            int idx = System.Math.Min(_insertedAtIndex, _acls.AnimationChains.Count);
            _acls.AnimationChains.Insert(idx, _chain);
            AppCommands.Self.RefreshTreeView();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
            AppCommands.Self.SaveCurrentAnimationChainList();
        }
    }
}
