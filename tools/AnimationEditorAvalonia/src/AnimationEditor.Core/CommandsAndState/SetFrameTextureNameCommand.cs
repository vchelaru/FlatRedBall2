using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    internal sealed class SetFrameTextureNameCommand : IUndoableCommand
    {
        private readonly AnimationFrameSave _frame;
        private readonly string? _oldName;
        private readonly string? _newName;
        private readonly IAppCommands _commands;
        private readonly IApplicationEvents _events;

        public string Description { get; }

        public SetFrameTextureNameCommand(AnimationFrameSave frame, string? oldName, string? newName,
            IAppCommands commands, IApplicationEvents events)
        {
            _frame = frame;
            _oldName = oldName;
            _newName = newName;
            _commands = commands;
            _events = events;
            string? name = newName ?? oldName;
            Description = name is not null
                ? $"Set Texture: {System.IO.Path.GetFileName(name.Replace('\\', '/'))}"
                : "Set Texture";
        }

        public bool Do()
        {
            var result = Apply(_newName);
            // Arm the wireframe to fit the frame into view on the next texture load in case the new
            // texture makes its region larger than the viewport (#616). Undo does not re-arm — reverting
            // a texture shouldn't surprise-zoom the user.
            _events.RaiseFitFrameToViewRequested();
            return result;
        }

        public void Undo() => Apply(_oldName);

        // Assigns the name verbatim — null stays null, "" stays "" — so "clear the
        // texture" round-trips exactly. Always returns true: a same-value reassignment
        // is filtered out by the caller before a command is ever constructed.
        private bool Apply(string? name)
        {
            _frame.TextureName = name!;
            _commands.RefreshTreeNode(_frame);
            _events.RaiseAnimationChainsChanged();
            return true;
        }
    }
}
