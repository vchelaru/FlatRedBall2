namespace AnimationEditor.Core.CommandsAndState.Commands
{
    public interface IUndoableCommand
    {
        void Undo();
        void Redo();
    }
}
