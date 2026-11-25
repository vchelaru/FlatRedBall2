using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Renames chains and frame shapes (#1811). Names are trimmed; chain names are unique per file and
/// shape names unique per frame. Never touches selection. A change is one undo step and
/// <see cref="EditResult.ChangedItems"/> holds the new name; renaming to the current name succeeds
/// with no change.
/// </summary>
public static class RenameEdit
{
    /// <summary>
    /// Empty fails with <see cref="EditErrorCode.EmptyName"/>, except that in a native-tsx project it
    /// reverts the chain to its auto "ID:{tileId}" name. A name another chain holds fails with
    /// <see cref="EditErrorCode.DuplicateName"/>; one XML cannot store with <see cref="EditErrorCode.InvalidValue"/>.
    /// </summary>
    public static EditResult RenameChain(
        IProjectManager projectManager, IAppCommands commands, AnimationChainSave chain, string newName)
    {
        newName = newName.Trim();
        bool NameTaken(string name) =>
            projectManager.AnimationChainListSave?.AnimationChains.Any(c => !ReferenceEquals(c, chain) && c.Name == name) == true;
        EditResult Duplicate(string name) => Fail(EditErrorCode.DuplicateName, $"A chain named '{name}' already exists.");

        if (newName.Length == 0)
        {
            // Two chains on the same tile, of which only one can be stored there, would get the same name.
            if (!projectManager.IsChainNameAuto(chain) && projectManager.GetTsxOwnerTileId(chain) is { } tileId
                && TiledAnimationToAchjMapper.SyntheticChainName(tileId) is var autoName && NameTaken(autoName))
                return Duplicate(autoName);

            return commands.RenameChain(chain, newName)
                ? Ok()
                : Fail(EditErrorCode.EmptyName, "Chain name cannot be empty.");
        }

        if (newName == chain.Name) return Ok();
        if (StorableText.Problem(newName) is { } problem) return Fail(EditErrorCode.InvalidValue, problem);

        if (NameTaken(newName)) return Duplicate(newName);

        return commands.RenameChain(chain, newName)
            ? Ok(newName)
            : Fail(EditErrorCode.Refused, $"Could not rename chain to '{newName}'.");
    }

    /// <summary>
    /// Renames a rect, circle or polygon, leaving its position and size as they are. Empty fails with
    /// <see cref="EditErrorCode.EmptyName"/>; a name another shape of the same frame holds, of any type,
    /// fails with <see cref="EditErrorCode.DuplicateName"/>; one XML cannot store with <see cref="EditErrorCode.InvalidValue"/>.
    /// </summary>
    public static EditResult RenameShape(
        IAppCommands commands, IObjectFinder objectFinder, ShapeSave shape, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0) return Fail(EditErrorCode.EmptyName, "Shape name cannot be empty.");
        if (newName == shape.Name) return Ok();
        if (StorableText.Problem(newName) is { } problem) return Fail(EditErrorCode.InvalidValue, problem);

        var frame = objectFinder.GetAnimationFrameContaining(shape);
        if (frame?.ShapesSave?.Shapes.Any(s => !ReferenceEquals(s, shape) && NameOf(s) == newName) == true)
            return Fail(EditErrorCode.DuplicateName, $"A shape named '{newName}' already exists in this frame.");

        switch (shape)
        {
            case AARectSave rect:
                commands.SetRectProps(frame, rect, newName, rect.X, rect.Y, rect.ScaleX, rect.ScaleY);
                break;
            case CircleSave circle:
                commands.SetCircleProps(frame, circle, newName, circle.X, circle.Y, circle.Radius);
                break;
            case PolygonSave polygon:
                commands.SetPolygonProps(frame, polygon, newName, polygon.X, polygon.Y);
                break;
            default:
                return Fail(EditErrorCode.WrongShapeType, $"Cannot rename a {shape.GetType().Name}.");
        }
        return Ok(newName);
    }

    private static string? NameOf(object shape) => (shape as ShapeSave)?.Name;

    private static EditResult Ok(params string[] changed) => new(changed, [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
