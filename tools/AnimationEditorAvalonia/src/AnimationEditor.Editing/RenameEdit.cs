using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Renames chains and frame shapes (#1811). Never touches selection.</summary>
public static class RenameEdit
{
    public static EditResult RenameChain(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, string newName) =>
        throw new NotImplementedException();

    public static EditResult RenameShape(
        IAppCommands commands, IObjectFinder objectFinder, ShapeSave shape, string newName) =>
        throw new NotImplementedException();
}
