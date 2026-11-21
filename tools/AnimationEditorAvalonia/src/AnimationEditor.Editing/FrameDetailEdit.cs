using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Keyframes;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Color parts to clear back to inherited on a frame; at least one must be set.</summary>
public readonly record struct FrameColorClear(
    bool Red = false, bool Green = false, bool Blue = false, bool Alpha = false, bool Operation = false);

/// <summary>
/// Per-frame and per-shape detail edits (#1811): frame events, frame and shape keyframes and curves, shape
/// order within a frame, and clearing color parts back to inherited. Never touches selection; each change is
/// one undo step. A value already in place is a successful no-op with no <see cref="EditResult.ChangedItems"/>.
/// Everything on a locked chain fails with <see cref="EditErrorCode.Locked"/>. Changed items are the frame
/// address (<c>Walk/1</c>), the event address (<c>Walk/1/event/0</c>, see <see cref="EditAddress"/>) or the
/// shape address.
/// </summary>
public static class FrameDetailEdit
{
    // ---- events ----

    /// <summary>Appends an event to the frame. Blank <paramref name="data"/> stores none.</summary>
    public static EditResult AddFrameEvent(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, string name, string? data)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        if (string.IsNullOrWhiteSpace(name)) return Fail(EditErrorCode.EmptyName, "An event needs a name.");

        int before = frame.Events.Count;
        commands.AddFrameEvent(frame, name, data);
        return frame.Events.Count == before + 1
            ? Ok(EventAddress(chain, frame, before))
            : Fail(EditErrorCode.Refused, "The event could not be added to this frame.");
    }

    /// <summary>Changes an event's name and/or data; an omitted part is kept, empty <paramref name="data"/> clears it.</summary>
    public static EditResult SetFrameEvent(
        IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, int index, string? name, string? data)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        if (name is null && data is null) return Fail(EditErrorCode.InvalidValue, "An event edit needs a name, data or both.");
        if (name is not null && string.IsNullOrWhiteSpace(name)) return Fail(EditErrorCode.EmptyName, "An event name cannot be empty.");
        var range = CheckEventIndex(chain, frame, index);
        if (range is not null) return range;

        var current = frame.Events[index];
        var newName = name ?? current.Name;
        var newData = data is null ? current.Data : string.IsNullOrWhiteSpace(data) ? null : data;
        if (newName == current.Name && newData == current.Data) return Ok();

        commands.SetFrameEvent(frame, index, newName, newData);
        return Ok(EventAddress(chain, frame, index));
    }

    /// <summary>Removes the event at <paramref name="index"/>; later events shift down.</summary>
    public static EditResult DeleteFrameEvent(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, int index)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        var range = CheckEventIndex(chain, frame, index);
        if (range is not null) return range;

        commands.RemoveFrameEvent(frame, index);
        return Ok(EventAddress(chain, frame, index));
    }

    // ---- frame keyframes ----

    /// <summary>Turns a frame's offset or color keyframe on or off. A chain's first frame is always a keyframe.</summary>
    public static EditResult SetFrameKeyframe(
        IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, KeyframeGroup group, bool isKeyframe)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;

        bool current = KeyframeInspectorState.IsEffectivelyKeyframe(group, frame, chain);
        if (current == isKeyframe) return Ok();
        if (KeyframeInspectorState.IsFirstFrame(frame, chain))
            return Fail(EditErrorCode.Refused, $"{Address(chain, frame)} is the chain's first frame, which is always a {Word(group)} keyframe.");

        commands.SetFrameKeyframe([frame], group, isKeyframe);
        return Ok(Address(chain, frame));
    }

    /// <summary>Sets the curve from a frame's offset or color keyframe to the next one. The frame must be a keyframe of that group.</summary>
    public static EditResult SetFrameKeyframeCurve(
        IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, KeyframeGroup group, KeyframeCurveChoice curve)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        if (!KeyframeInspectorState.IsEffectivelyKeyframe(group, frame, chain))
            return Fail(EditErrorCode.Refused,
                $"{Address(chain, frame)} is not a {Word(group)} keyframe, so it has no curve; turn the keyframe on first (set-frame-keyframe).");

        var (type, easing) = group == KeyframeGroup.Offset
            ? (frame.OffsetInterpolation, frame.OffsetEasing)
            : (frame.ColorInterpolation, frame.ColorEasing);
        if (KeyframeCurvePicker.FromStored(type, easing) == curve) return Ok();

        commands.SetFrameKeyframeCurve([frame], group, curve);
        return Ok(Address(chain, frame));
    }

    // ---- shape keyframes ----

    /// <summary>Turns a shape's offset, size or points keyframe on or off. The first shape of a run of frames holding its name is always a keyframe.</summary>
    public static EditResult SetShapeKeyframe(
        IAppCommands commands, IObjectFinder finder, ShapeSave shape, ShapeKeyframeGroup group, bool isKeyframe)
    {
        var owner = ShapeEdit.Owner(finder, shape);
        if (owner.Error is not null) return owner.Error;
        var missing = CheckGroup(owner, shape, group);
        if (missing is not null) return missing;

        if (KeyframeInspectorState.IsEffectivelyKeyframe(group, shape, owner.Chain) == isKeyframe) return Ok();
        if (KeyframeInspectorState.IsRunStart(shape, owner.Chain))
            return Fail(EditErrorCode.Refused, $"'{ShapeAddress(owner, shape)}' is the first of its run of frames, which is always a {Word(group)} keyframe.");

        commands.SetShapeKeyframe([shape], group, isKeyframe);
        return Ok(ShapeAddress(owner, shape));
    }

    /// <summary>Sets the curve from a shape's offset, size or points keyframe to the next one. The shape must be a keyframe of that group.</summary>
    public static EditResult SetShapeKeyframeCurve(
        IAppCommands commands, IObjectFinder finder, ShapeSave shape, ShapeKeyframeGroup group, KeyframeCurveChoice curve)
    {
        var owner = ShapeEdit.Owner(finder, shape);
        if (owner.Error is not null) return owner.Error;
        var missing = CheckGroup(owner, shape, group);
        if (missing is not null) return missing;
        if (!KeyframeInspectorState.IsEffectivelyKeyframe(group, shape, owner.Chain))
            return Fail(EditErrorCode.Refused,
                $"'{ShapeAddress(owner, shape)}' is not a {Word(group)} keyframe, so it has no curve; turn the keyframe on first (set-shape-keyframe).");

        var (type, easing) = StoredCurve(shape, group);
        if (KeyframeCurvePicker.FromStored(type, easing) == curve) return Ok();

        commands.SetShapeKeyframeCurve([shape], group, curve);
        return Ok(ShapeAddress(owner, shape));
    }

    // ---- shape order ----

    /// <summary>
    /// Moves a shape so it ends up at <paramref name="index"/> among the shapes of its own type in the frame
    /// (files store shapes grouped by type, so a shape never crosses types).
    /// </summary>
    public static EditResult ReorderShape(IAppCommands commands, IObjectFinder finder, ShapeSave shape, int index)
    {
        var owner = ShapeEdit.Owner(finder, shape);
        if (owner.Error is not null) return owner.Error;

        var all = owner.Frame!.ShapesSave!.Shapes.ToList();
        var group = Enumerable.Range(0, all.Count).Where(i => all[i].GetType() == shape.GetType()).ToList();
        if (index < 0 || index >= group.Count)
            return Fail(EditErrorCode.IndexOutOfRange,
                $"Shape index {index} is out of range: the frame has {group.Count} {EditAddress.TypeName(shape)} shape(s).");

        int from = all.IndexOf(shape);
        int target = group[0] + index;
        if (target == from) return Ok();

        commands.MoveShapeToIndex(shape, owner.Frame, target > from ? target + 1 : target);
        return Ok(ShapeAddress(owner, shape));
    }

    // ---- color ----

    /// <summary>Clears the chosen color parts back to inherited from the previous frames, in one undo step.</summary>
    public static EditResult ClearFrameColor(IAppCommands commands, AnimationChainSave chain, AnimationFrameSave frame, FrameColorClear clear)
    {
        var check = CheckFrame(chain, frame);
        if (check is not null) return check;
        if (clear == default) return Fail(EditErrorCode.InvalidValue, "A clear needs at least one of red, green, blue, alpha, operation.");

        bool anySet = (clear.Red && frame.Red is not null) || (clear.Green && frame.Green is not null)
            || (clear.Blue && frame.Blue is not null) || (clear.Alpha && frame.Alpha is not null)
            || (clear.Operation && frame.ColorOperation is not null);
        if (!anySet) return Ok();

        commands.ClearFrameColorValues([frame], clear.Red, clear.Green, clear.Blue, clear.Alpha, clear.Operation);
        return Ok(Address(chain, frame));
    }

    // ---- helpers ----

    private static EditResult? CheckFrame(AnimationChainSave chain, AnimationFrameSave frame)
    {
        if (!chain.Frames.Contains(frame)) return Fail(EditErrorCode.NotFound, $"The frame is not in chain '{chain.Name}'.");
        return chain.IsLocked ? StructureEdit.LockedFail(chain) : null;
    }

    private static EditResult? CheckEventIndex(AnimationChainSave chain, AnimationFrameSave frame, int index) =>
        index >= 0 && index < frame.Events.Count
            ? null
            : Fail(EditErrorCode.IndexOutOfRange,
                $"Event index {index} is out of range: {Address(chain, frame)} has {frame.Events.Count} event(s).");

    private static EditResult? CheckGroup(
        (AnimationChainSave? Chain, AnimationFrameSave? Frame, EditResult? Error) owner, ShapeSave shape, ShapeKeyframeGroup group) =>
        KeyframeInspectorState.HasGroup(group, shape)
            ? null
            : Fail(EditErrorCode.Refused, $"'{ShapeAddress(owner, shape)}' has no {Word(group)} keyframe: " +
                (group == ShapeKeyframeGroup.Points ? "only polygons have points." : "polygons have no size, use points."));

    private static (string? Type, string? Easing) StoredCurve(ShapeSave shape, ShapeKeyframeGroup group) => group switch
    {
        ShapeKeyframeGroup.Offset => (shape.OffsetInterpolation, shape.OffsetEasing),
        ShapeKeyframeGroup.Size => shape is SizedShapeSave s ? (s.SizeInterpolation, s.SizeEasing) : (null, null),
        _ => shape is PolygonSave p ? (p.PointsInterpolation, p.PointsEasing) : (null, null),
    };

    private static string Word(KeyframeGroup group) => group.ToString().ToLowerInvariant();

    private static string Word(ShapeKeyframeGroup group) => group.ToString().ToLowerInvariant();

    private static string Address(AnimationChainSave chain, AnimationFrameSave frame) => $"{chain.Name}/{chain.Frames.IndexOf(frame)}";

    private static string EventAddress(AnimationChainSave chain, AnimationFrameSave frame, int index) =>
        $"{Address(chain, frame)}/event/{index}";

    private static string ShapeAddress((AnimationChainSave? Chain, AnimationFrameSave? Frame, EditResult? Error) owner, ShapeSave shape) =>
        ShapeEdit.Address(owner.Chain!, owner.Frame!, EditAddress.TypeName(shape), shape.Name);

    private static EditResult Ok(params string[] changed) => new(changed, [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
