namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>A delete that can also be done to the document of a background tab, to be undone once that tab is in front.</summary>
    internal interface IDeleteCommand : IUndoableCommand
    {
        /// <summary><see cref="IUndoableCommand.Do"/> without showing or saving anything.</summary>
        /// <returns>False when there was nothing to remove.</returns>
        bool Remove();
    }
}
