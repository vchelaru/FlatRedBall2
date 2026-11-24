using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>
/// Adds, deletes, duplicates and edits frame shapes (#1811). Never touches selection; each change is one
/// undo step. A shape in a locked chain fails with <see cref="EditErrorCode.Locked"/>. A value already in place
/// is a successful no-op with no <see cref="EditResult.ChangedItems"/>, so a no-change batch leaves the file
/// byte-identical. <see cref="EditResult.ChangedItems"/> holds the shape address
/// (<c>Walk/2/rect/Hitbox</c>, see <see cref="EditAddress"/>) of what the operation produced, changed or removed.
/// </summary>
public static class ShapeEdit
{
    /// <summary>
    /// Appends a shape of <paramref name="type"/> (rect, circle or polygon) with the editor's default size, centred on
    /// the frame's offset. <paramref name="name"/> null picks the editor's default name made unique in the frame; a
    /// given name must be non-empty, free of '/', and unused by any shape of the frame.
    /// </summary>
    public static EditResult AddShape(
        IAppCommands commands, IObjectFinder finder, AnimationFrameSave frame, string type, string? name)
    {
        var defaultName = type switch
        {
            "rect" => NewShapeDefaults.RectName,
            "circle" => NewShapeDefaults.CircleName,
            "polygon" => NewShapeDefaults.PolygonName,
            _ => null,
        };
        if (defaultName is null)
            return Fail(EditErrorCode.InvalidAddress, $"Shape type '{type}' is not one of rect|circle|polygon.");

        var chain = finder.GetAnimationChainContaining(frame);
        if (chain is null) return Fail(EditErrorCode.NotFound, "The frame is not in the file.");
        if (chain.IsLocked) return StructureEdit.LockedFail(chain);

        var existing = frame.ShapesSave?.Shapes.OfType<ShapeSave>().Select(s => s.Name).ToList() ?? [];
        if (name is null)
        {
            name = StringFunctions.MakeStringUnique(defaultName, existing);
        }
        else
        {
            name = name.Trim();
            if (name.Length == 0) return Fail(EditErrorCode.EmptyName, "Shape name cannot be empty.");
            if (name.Contains('/')) return Fail(EditErrorCode.InvalidAddress, $"Shape name '{name}' cannot contain '/'.");
            if (existing.Contains(name))
                return Fail(EditErrorCode.DuplicateName, $"A shape named '{name}' already exists in this frame.");
        }

        ShapeSave shape = type switch
        {
            "rect" => NewShapeDefaults.Rect(frame, name),
            "circle" => NewShapeDefaults.Circle(frame, name),
            _ => NewShapeDefaults.Polygon(frame, name),
        };
        return commands.AddShape(frame, shape)
            ? Ok(Address(chain, frame, type, name))
            : Fail(EditErrorCode.Refused, $"Could not add a {type} to the frame.");
    }

    public static EditResult DeleteShape(IAppCommands commands, IObjectFinder finder, ShapeSave shape)
    {
        var owner = Owner(finder, shape);
        if (owner.Error is not null) return owner.Error;

        var address = Address(owner.Chain!, owner.Frame!, EditAddress.TypeName(shape), shape.Name);
        commands.DeleteShapes([shape]);
        return Ok(address);
    }

    /// <summary>Appends a copy of <paramref name="shape"/> to its frame under a unique name (Hitbox becomes Hitbox2).</summary>
    public static EditResult DuplicateShape(IAppCommands commands, IObjectFinder finder, ShapeSave shape)
    {
        var owner = Owner(finder, shape);
        if (owner.Error is not null) return owner.Error;

        return commands.DuplicateShape(shape) is ShapeSave copy
            ? Ok(Address(owner.Chain!, owner.Frame!, EditAddress.TypeName(copy), copy.Name))
            : Fail(EditErrorCode.Refused, $"Could not duplicate shape '{shape.Name}'.");
    }

    /// <summary>
    /// Appends a copy of <paramref name="shape"/>, under the same name, to every frame of <paramref name="targets"/>.
    /// A target that already has a shape of that name fails. Only the target chains must be unlocked.
    /// </summary>
    public static EditResult DuplicateShapeTo(IAppCommands commands, ShapeSave shape, IReadOnlyList<FrameRef> targets)
    {
        foreach (var (chain, frame) in targets)
        {
            if (chain.IsLocked) return StructureEdit.LockedFail(chain);
            if (frame.ShapesSave?.Shapes.OfType<ShapeSave>().Any(s => s.Name == shape.Name) == true)
                return Fail(EditErrorCode.DuplicateName, $"A shape named '{shape.Name}' already exists in {chain.Name}/{chain.Frames.IndexOf(frame)}.");
        }

        commands.PasteShapes(targets.Select(t => t.Frame).ToList(), [shape]);
        return Ok(targets.Select(t => Address(t.Chain, t.Frame, EditAddress.TypeName(shape), shape.Name)).ToArray());
    }

    /// <summary>Sets any of a rectangle's center and half-extents; a null field is left as it is.</summary>
    public static EditResult SetRect(
        IAppCommands commands, IObjectFinder finder, AARectSave rect, float? x, float? y, float? scaleX, float? scaleY)
    {
        var owner = Owner(finder, rect);
        if (owner.Error is not null) return owner.Error;
        if (x is null && y is null && scaleX is null && scaleY is null)
            return Fail(EditErrorCode.InvalidValue, "A rect edit needs at least one of x, y, scaleX, scaleY.");
        var bad = CheckPosition(x, y) ?? CheckPositive(scaleX, "scaleX") ?? CheckPositive(scaleY, "scaleY");
        if (bad is not null) return bad;

        var (nx, ny, nsx, nsy) = (x ?? rect.X, y ?? rect.Y, scaleX ?? rect.ScaleX, scaleY ?? rect.ScaleY);
        if (nx == rect.X && ny == rect.Y && nsx == rect.ScaleX && nsy == rect.ScaleY) return Ok();

        commands.SetRectProps(owner.Frame, rect, rect.Name, nx, ny, nsx, nsy);
        return Ok(Address(owner.Chain!, owner.Frame!, "rect", rect.Name));
    }

    /// <summary>Centers a rectangle on its frame's offset and gives it the frame's displayed size; its region size must be known.</summary>
    public static EditResult MatchFrameSize(IAppCommands commands, IObjectFinder finder, AARectSave rect)
    {
        var owner = Owner(finder, rect);
        if (owner.Error is not null) return owner.Error;
        if (PropertyEdit.CheckRegionSize(commands, owner.Chain!, owner.Frame!) is { } bad) return bad;

        var before = (rect.X, rect.Y, rect.ScaleX, rect.ScaleY);
        commands.MatchRectangleToFrame(rect, owner.Frame!);
        return (rect.X, rect.Y, rect.ScaleX, rect.ScaleY) == before ? Ok() : Ok(Address(owner.Chain!, owner.Frame!, "rect", rect.Name));
    }

    /// <summary>Sets any of a circle's center and radius; a null field is left as it is.</summary>
    public static EditResult SetCircle(
        IAppCommands commands, IObjectFinder finder, CircleSave circle, float? x, float? y, float? radius)
    {
        var owner = Owner(finder, circle);
        if (owner.Error is not null) return owner.Error;
        if (x is null && y is null && radius is null)
            return Fail(EditErrorCode.InvalidValue, "A circle edit needs at least one of x, y, radius.");
        var bad = CheckPosition(x, y) ?? CheckPositive(radius, "radius");
        if (bad is not null) return bad;

        var (nx, ny, nr) = (x ?? circle.X, y ?? circle.Y, radius ?? circle.Radius);
        if (nx == circle.X && ny == circle.Y && nr == circle.Radius) return Ok();

        commands.SetCircleProps(owner.Frame, circle, circle.Name, nx, ny, nr);
        return Ok(Address(owner.Chain!, owner.Frame!, "circle", circle.Name));
    }

    /// <summary>
    /// Sets a polygon's origin and/or replaces its points. The points are stored exactly as given: the editor's
    /// closed outlines repeat the first point at the end, so give that point too to keep one closed. At least 3 points.
    /// </summary>
    public static EditResult SetPolygon(
        IAppCommands commands, IObjectFinder finder, PolygonSave polygon, float? x, float? y,
        IReadOnlyList<(float X, float Y)>? points)
    {
        var owner = Owner(finder, polygon);
        if (owner.Error is not null) return owner.Error;
        if (x is null && y is null && points is null)
            return Fail(EditErrorCode.InvalidValue, "A polygon edit needs at least one of x, y, points.");
        var bad = CheckPosition(x, y);
        if (bad is not null) return bad;
        if (points is not null)
        {
            if (points.Count < 3)
                return Fail(EditErrorCode.InvalidValue, $"A polygon needs at least 3 points; {points.Count} given.");
            if (points.Any(p => !NumberFields.InRange(p.X) || !NumberFields.InRange(p.Y)))
                return Fail(EditErrorCode.InvalidValue, $"A polygon point is out of range: valid values are {NumberFields.RangeText}.");
        }

        var (nx, ny) = (x ?? polygon.X, y ?? polygon.Y);
        bool samePoints = points is null
            || (points.Count == polygon.Points.Count
                && points.Select((p, i) => p.X == polygon.Points[i].X && p.Y == polygon.Points[i].Y).All(same => same));
        if (nx == polygon.X && ny == polygon.Y && samePoints) return Ok();

        commands.SetPolygonGeometry(polygon, nx, ny,
            points?.Select(p => new Vector2Save { X = p.X, Y = p.Y }).ToList());
        return Ok(Address(owner.Chain!, owner.Frame!, "polygon", polygon.Name));
    }

    /// <summary>Sets the position every shape type has (rect and circle center, polygon origin); a null field is left as it is.</summary>
    public static EditResult SetCommon(IAppCommands commands, IObjectFinder finder, ShapeSave shape, float? x, float? y)
    {
        var owner = Owner(finder, shape);
        if (owner.Error is not null) return owner.Error;
        if (x is null && y is null) return Fail(EditErrorCode.InvalidValue, "A shape edit needs at least one of x, y.");
        var bad = CheckPosition(x, y);
        if (bad is not null) return bad;

        if ((x is null || x == shape.X) && (y is null || y == shape.Y)) return Ok();

        commands.SetShapePropsBulk([shape], null,
            x is { } nx ? NumericEdit.Set((decimal)nx) : null,
            y is { } ny ? NumericEdit.Set((decimal)ny) : null);
        return Ok(Address(owner.Chain!, owner.Frame!, EditAddress.TypeName(shape), shape.Name));
    }

    internal static (AnimationChainSave? Chain, AnimationFrameSave? Frame, EditResult? Error) Owner(IObjectFinder finder, ShapeSave shape)
    {
        var frame = finder.GetAnimationFrameContaining(shape);
        var chain = frame is null ? null : finder.GetAnimationChainContaining(frame);
        if (frame is null || chain is null) return (null, null, Fail(EditErrorCode.NotFound, $"Shape '{shape.Name}' is not in the file."));
        if (chain.IsLocked) return (null, null, StructureEdit.LockedFail(chain));
        return (chain, frame, null);
    }

    private static EditResult? CheckPosition(float? x, float? y) =>
        (x is { } a && !NumberFields.InRange(a)) || (y is { } b && !NumberFields.InRange(b))
            ? Fail(EditErrorCode.InvalidValue, $"x or y is out of range: valid values are {NumberFields.RangeText}.")
            : null;

    private static EditResult? CheckPositive(float? value, string field) =>
        value is { } v && !(NumberFields.InRange(v) && v > 0f)
            ? Fail(EditErrorCode.InvalidValue, $"{field} must be greater than zero and at most {NumberFields.Max} (a shape's scale and radius are half-extents).")
            : null;

    internal static string Address(AnimationChainSave chain, AnimationFrameSave frame, string type, string name) =>
        $"{chain.Name}/{chain.Frames.IndexOf(frame)}/{type}/{name}";

    private static EditResult Ok(params string[] changed) => new(changed, [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
