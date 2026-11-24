using System.Globalization;
using System.Text.Json;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <param name="Type">JSON type of the value: "string", "integer" or "number".</param>
/// <param name="Values">The allowed values when the field is an enumeration.</param>
/// <param name="Relative">True when a number field also takes a <see cref="NumericEdit"/> text relative to the target's current value (<c>+2</c>).</param>
public sealed record OpField(string Name, string Type, bool Required, string Description, IReadOnlyList<string>? Values = null, bool Relative = false);

public sealed record OpSpec(string Name, string Description, IReadOnlyList<OpField> Fields);

/// <summary>One validated operation of an ops file, in file order.</summary>
public sealed record EditOp(int Index, OpSpec Spec, IReadOnlyDictionary<string, string> Values);

/// <summary>The ops of a file, or why the file is not a valid ops file.</summary>
public sealed record OpsParse(IReadOnlyList<EditOp>? Ops, string? Error);

/// <summary>
/// Outcome of applying an ops file. <see cref="Result"/> lists what changed and what failed;
/// <see cref="ErrorOps"/> holds, parallel to <c>Result.Errors</c>, the index of the failing op (null for
/// a failure that belongs to no single op, such as a failed final check).
/// </summary>
public sealed record EditRunResult(EditResult Result, IReadOnlyList<int?> ErrorOps)
{
    public bool Success => Result.Success;
}

/// <summary>
/// The operations an ops file can hold (#1811). An ops file is <c>{"ops":[{"op":"rename-chain",...}, ...]}</c>,
/// applied in order, so a later op addresses items by the names earlier ops gave them. Addresses are described on
/// <see cref="EditAddress"/>. <see cref="Specs"/> is the single source for validation and for <c>--schema</c>.
/// </summary>
public static class EditOps
{
    private static readonly IReadOnlyList<string> CopyValues = ["keep", "copy", "overwrite"];
    private static readonly IReadOnlyList<string> ColorOperationValues = ["multiply", "add"];
    private static readonly IReadOnlyList<string> UnitValues = ["second", "millisecond"];
    private static readonly IReadOnlyList<string> ShapeTypeValues = ["rect", "circle", "polygon"];
    private static readonly IReadOnlyList<string> ChainLengthModeValues = ["proportional", "even"];
    private static readonly IReadOnlyList<string> FrameGroupValues = ["offset", "color"];
    private static readonly IReadOnlyList<string> ShapeGroupValues = ["offset", "size", "points"];
    private static readonly IReadOnlyList<string> CurveValues =
        ["snap", "linear", "ease-in", "ease-out", "ease-in-out", "anticipate", "overshoot", "bounce"];
    private const string EventAddressDescription = "Event address: Chain/frameIndex/event/eventIndex (zero-based).";
    private const string EventSelectorDescription = "Event address or selector: Chain/frameIndex/event/eventIndex (zero-based), such as */*/event/*.";
    private const string TargetFramesDescription = "Destination frames: a frame address or frame selector (Walk/2-5, Attack*/*). Default: the source's own frame. Must be unlocked; the source's chain may be locked.";
    private const string ShapeAddressDescription = "Shape address: Chain/frameIndex/rect|circle|polygon/name.";
    /// <summary>How <c>--schema</c> explains bulk addressing; shared by the text and JSON output.</summary>
    public const string SelectorSyntax =
        "Bulk selectors: where an op takes a frame address, the index may be * (every frame), a range (Walk/2-5), "
        + "or a list of numbers and ranges (Walk/0,3,7 or Walk/0,2-3,6), and the chain may be a glob with * and ? (Walk*/0-2). "
        + "Shape addresses take the same frame selector, and the shape name may be * or a glob (Walk/*/rect/Hitbox). "
        + "Event addresses take the same frame selector, and the event index takes the same forms as a frame index (*/*/event/*, Walk/0/event/1-2). "
        + "Chain ops delete-chain, set-chain-loop, set-chain-pingpong, set-chain-reverse, set-chain-locked, set-chain-length, invert-frame-order and justify-offsets-bottom take a glob (Walk*, *). "
        + "Selectors work on every op that addresses a frame, event or shape except reorder-frame, reorder-shape, rename-shape and the add-/duplicate-/reorder-/rename-chain ops; those take one exact address. "
        + "A selector expands when its op starts, in chain order then in the order written; delete-frame, duplicate-frame and delete-frame-event run from the highest index down so every index means the state before the op. "
        + "A selector that matches nothing is an error (NoMatch), as is any index past a chain's end. Any failing target fails the whole batch and nothing is written. "
        + "Shape and event selectors skip frames that lack the shape or event. A chain whose name really contains * or ? cannot be addressed by glob.";

    /// <summary>How <c>--schema</c> explains relative values; shared by the text and JSON output.</summary>
    public const string RelativeSyntax =
        "Relative values: a field marked relative also takes a value starting with an operator, as the Inspector does: +2, \"- 2\" (a space after the minus; -2 is absolute), *2, /2. "
        + "Each target changes from its own value, read before the op changes any target, and the result must be in the field's range or the batch fails. "
        + "In JSON a number is absolute and a string is a relative value or arithmetic (\"3+4\"). Every other number is absolute. Numbers past 1000000 either way are refused.";

    /// <summary>How <c>--schema</c> explains Tiled .tsx files; shared by the text and JSON output.</summary>
    public static string TsxNote =>
        "Tiled tilesets (.tsx): describe, check and edit open a .tsx as a native project, the way the editor does; a tileset the editor cannot open is refused with its reason. "
        + $"A tile animation stores only frame rectangles and durations, so only these ops work on a .tsx: {string.Join(", ", TsxOpPolicy.Supported)}; every other op fails with NotSupportedInTsx. "
        + "add-frame needs a region and the tileset's own image. Chains map to tiles: a chain whose frames are not whole tiles, or two chains on one owner tile, fail the batch. "
        + "Everything else in the file (wangsets, object layers, properties, other tiles) is preserved on save, and a batch that changes nothing leaves the file untouched.";

    private const string RegionDescription = "Pixel rectangle of the texture, as x,y,width,height.";

    public static IReadOnlyList<OpSpec> Specs { get; } =
    [
        new("rename-chain", "Renames a chain. The new name must be unique in the file.",
        [
            new("chain", "string", true, "Chain name."),
            new("to", "string", true, "New chain name."),
        ]),
        new("rename-shape", "Renames a rect, circle or polygon. The new name must be unique in its frame.",
        [
            new("shape", "string", true, "Shape address: Chain/frameIndex/rect|circle|polygon/name."),
            new("to", "string", true, "New shape name."),
        ]),
        new("set-frame-texture", "Points one frame at a texture file. The file must exist and be a PNG, JPEG, GIF or BMP image.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("texture", "string", true, "Texture path, relative to the animation file or absolute."),
            new("copy", "string", false, CopyDescription, CopyValues),
        ]),
        new("add-chain", "Appends an empty chain. The name must be unique in the file and cannot contain '/'.",
        [
            new("name", "string", true, "New chain name."),
        ]),
        new("delete-chain", "Deletes a chain and its frames.",
        [
            new("chain", "string", true, "Chain name, or a glob (Walk*) to delete every match."),
        ]),
        new("duplicate-chain", "Inserts a copy of a chain right after it, frames and shapes included.",
        [
            new("chain", "string", true, "Chain name."),
            new("name", "string", false, "Name of the copy, unique in the file. Default: the source name with a number added (Walk becomes Walk2)."),
        ]),
        new("import-chain", "Appends copies of chains of another .achx/.achj, at the end, in the source file's order, with everything the chain stores (flags, lock, frames, shapes, events). Textures are referenced where they are and lengths converted to this file's unit; the source file is only read. A name this file already has fails the batch listing every clash, and nothing is written. A check error the chain has in its source (a missing texture, two shapes of one name) fails the batch like any new check error: fix the source first, or set the texture later in the batch. Not for a .tsx.",
        [
            new("chain", "string", true, "Chain name in the source file, or a glob (Walk*) to import every match."),
            new("from", "string", true, "Source .achx/.achj, relative to the animation file or absolute. Not the file itself (use duplicate-chain)."),
            new("name", "string", false, "Name of the copy, unique in the file; only when chain matches a single chain."),
        ]),
        new("reorder-chain", "Moves a chain so it ends up at the given position in the file.",
        [
            new("chain", "string", true, "Chain name."),
            new("index", "integer", true, "Final zero-based position, 0 to chain count - 1."),
        ]),
        new("add-frame", "Appends a frame to a chain. The texture must exist and be a PNG, JPEG, GIF or BMP image. Shows the whole texture unless region is given.",
        [
            new("chain", "string", true, "Chain name."),
            new("texture", "string", true, "Texture path, relative to the animation file or absolute."),
            new("region", "string", false, "Pixel rectangle of the texture to show, as x,y,width,height. PNG textures only."),
            new("length", "number", false, "Frame length in the file's own time unit (see describe). Default: that unit's default length."),
            new("copy", "string", false, CopyDescription, CopyValues),
        ]),
        new("add-frames-grid", "Appends one frame per cell of a sprite sheet grid, row by row from the top-left. The texture must be an existing PNG and the whole grid must lie inside it.",
        [
            new("chain", "string", true, "Chain name."),
            new("texture", "string", true, "Texture path, relative to the animation file or absolute."),
            new("cellWidth", "integer", true, "Cell width in pixels, greater than 0."),
            new("cellHeight", "integer", true, "Cell height in pixels, greater than 0."),
            new("columns", "integer", true, "Cells per row, greater than 0."),
            new("rows", "integer", true, "Number of rows, greater than 0."),
            new("start", "integer", false, "First cell to add, numbered row by row from 0. Default 0."),
            new("count", "integer", false, "How many cells to add from start. Default: through the last cell."),
            new("frameLength", "number", false, "Length of each frame in the file's own time unit. Default: that unit's default length."),
            new("copy", "string", false, CopyDescription, CopyValues),
        ]),
        new("delete-frame", "Deletes one frame.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
        ]),
        new("duplicate-frame", "Inserts a copy of a frame: texture, region, length, offset, flips, color, keyframes, events, and shapes with their geometry and keyframes. Without to and index it lands right after the source; with either, the selected frames land as one block per destination chain, in the order written. Move a frame by duplicating it and deleting the source in the same batch.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex, or a frame selector (Walk/2-5)."),
            new("to", "string", false, "Destination chain, one exact name. Default: the source's own chain. Must be unlocked; the source's chain may be locked."),
            new("index", "integer", false, "Zero-based position in the destination, 0 to its frame count. Default with to: the end."),
        ]),
        new("reorder-frame", "Moves a frame so it ends up at the given position in its chain.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("index", "integer", true, "Final zero-based position, 0 to frame count - 1."),
        ]),
        new("set-chain-loop", "Turns looping on or off for a chain. Fails on a locked chain.",
        [
            new("chain", "string", true, "Chain name."),
            new("value", "boolean", true, "true to loop."),
        ]),
        new("set-chain-pingpong", "Turns ping-pong playback on or off for a chain. Fails on a locked chain.",
        [
            new("chain", "string", true, "Chain name."),
            new("value", "boolean", true, "true to play forward then backward."),
        ]),
        new("set-chain-reverse", "Turns reverse playback on or off for a chain. Fails on a locked chain.",
        [
            new("chain", "string", true, "Chain name."),
            new("value", "boolean", true, "true to play backward."),
        ]),
        new("set-chain-locked", "Locks or unlocks a chain. Locked chains reject every other edit of the chain and its frames.",
        [
            new("chain", "string", true, "Chain name."),
            new("value", "boolean", true, "true to lock."),
        ]),
        new("set-chain-length", "Rescales a chain's frame lengths so they add up to length, in the file's own time unit. That is one forward pass: describe shows the played cycle in seconds, twice as long for a ping-pong chain. Every resulting frame length must be in set-frame-length's range, and proportional needs a total above 0. Fails on a locked chain or one with no frames. A .tsx stores whole milliseconds, so its frames are rounded on save.",
        [
            new("chain", "string", true, "Chain name, or a glob (Walk*)."),
            new("length", "number", true, "Sum of the frame lengths in the file's time unit.", Relative: true),
            new("mode", "string", false, "proportional (default) keeps the ratios between frames; even gives every frame length / frame count.", ChainLengthModeValues),
        ]),
        new("invert-frame-order", "Reverses the order of a chain's frames (set-chain-reverse keeps the order and plays it backward). Fails on a locked chain.",
        [
            new("chain", "string", true, "Chain name, or a glob (Walk*)."),
        ]),
        new("justify-offsets-bottom", "Sets every frame's relative Y to half its displayed height (its region width under a diagonal flip), so each frame's bottom edge is at y = 0; X is left as it is. Fails on a locked chain, or when a frame's region size is unknown (whole texture, size not readable).",
        [
            new("chain", "string", true, "Chain name, or a glob (Walk*)."),
        ]),
        new("set-frame-length", "Sets one frame's length in the file's own time unit (see describe). Valid range: 0.001 to 60 seconds, or 1 to 60000 milliseconds.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("length", "number", true, "New length in the file's time unit.", Relative: true),
        ]),
        new("set-frame-region", "Sets the pixel rectangle of the texture a frame shows. The frame's texture must be an existing PNG and the rectangle must lie inside it.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("region", "string", true, RegionDescription),
        ]),
        new("set-frame-offset", "Sets a frame's relative offset. Give x, y or both; an omitted axis is left as it is.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("x", "number", false, "X offset.", Relative: true),
            new("y", "number", false, "Y offset.", Relative: true),
        ]),
        new("set-frame-color", "Sets a frame's color channels and operation. Give at least one; omitted ones are left as they are. A channel cannot be cleared back to inherited. A relative value on an unset channel starts from the value it inherits (255 when no earlier frame sets it, or 0 for red, green and blue in add).",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("red", "integer", false, "Red, -255 to 255.", Relative: true),
            new("green", "integer", false, "Green, -255 to 255.", Relative: true),
            new("blue", "integer", false, "Blue, -255 to 255.", Relative: true),
            new("alpha", "integer", false, "Alpha, 0 to 255.", Relative: true),
            new("operation", "string", false, "How red, green and blue combine with the texture.", ColorOperationValues),
        ]),
        new("set-frame-flip", "Sets a frame's flip flags to the given values (not a toggle). Give at least one; omitted ones are left as they are. A change also mirrors the frame's offset and shapes, as in the editor.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("flipH", "boolean", false, "Flip horizontally."),
            new("flipV", "boolean", false, "Flip vertically."),
            new("flipDiagonal", "boolean", false, "Flip along the diagonal."),
        ]),
        new("add-shape", "Appends a rect, circle or polygon to a frame with the editor's default size, centred on the frame's offset. Name defaults to a unique default name; a given name must be unused by any shape of the frame.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("type", "string", true, "Shape type.", ShapeTypeValues),
            new("name", "string", false, "Shape name, unique in the frame, no '/'."),
        ]),
        new("delete-shape", "Deletes one shape.",
        [
            new("shape", "string", true, ShapeAddressDescription),
        ]),
        new("duplicate-shape", "Appends a copy of a shape to its frame under a unique name (Hitbox becomes Hitbox2), or with to, to other frames under the same name. A target frame that already has that name fails the batch.",
        [
            new("shape", "string", true, "Shape address or selector: Chain/frameIndex/rect|circle|polygon/name."),
            new("to", "string", false, TargetFramesDescription),
        ]),
        new("set-rect", "Sets a rect's center and half-extents. Give at least one; omitted ones are left as they are.",
        [
            new("shape", "string", true, ShapeAddressDescription),
            new("x", "number", false, "Center X.", Relative: true),
            new("y", "number", false, "Center Y.", Relative: true),
            new("scaleX", "number", false, "Half-width, greater than 0.", Relative: true),
            new("scaleY", "number", false, "Half-height, greater than 0.", Relative: true),
        ]),
        new("match-frame-size", "Centers a rect on its frame's offset and sizes it to the frame as displayed (region width and height swapped under a diagonal flip). Fails when the region size is unknown (whole texture, size not readable).",
        [
            new("shape", "string", true, "Rect address or selector: Chain/frameIndex/rect/name."),
        ]),
        new("set-circle", "Sets a circle's center and radius. Give at least one; omitted ones are left as they are.",
        [
            new("shape", "string", true, ShapeAddressDescription),
            new("x", "number", false, "Center X.", Relative: true),
            new("y", "number", false, "Center Y.", Relative: true),
            new("radius", "number", false, "Radius, greater than 0.", Relative: true),
        ]),
        new("set-polygon", "Sets a polygon's origin and/or replaces its points. Give at least one; omitted ones are left as they are. The editor's closed outlines repeat the first point at the end; points are stored exactly as given.",
        [
            new("shape", "string", true, ShapeAddressDescription),
            new("x", "number", false, "Origin X.", Relative: true),
            new("y", "number", false, "Origin Y.", Relative: true),
            new("points", "points", false, "At least 3 points relative to the origin, Y+ up, as [[x,y],[x,y],...]."),
        ]),
        new("set-shape-common", "Sets the position every shape type has (rect and circle center, polygon origin). Give at least one; omitted ones are left as they are.",
        [
            new("shape", "string", true, ShapeAddressDescription),
            new("x", "number", false, "X.", Relative: true),
            new("y", "number", false, "Y.", Relative: true),
        ]),
        new("add-frame-event", "Appends a named event to a frame. Data is an optional free-form string the game interprets.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("name", "string", true, "Event name."),
            new("data", "string", false, "Optional payload."),
        ]),
        new("set-frame-event", "Changes an event's name and/or data. Give at least one; an omitted one is kept, empty data clears it.",
        [
            new("event", "string", true, EventAddressDescription),
            new("name", "string", false, "New event name."),
            new("data", "string", false, "New payload; empty clears it."),
        ]),
        new("duplicate-frame-event", "Appends a copy of an event to its frame, or with to, to other frames. A frame may hold several events of one name.",
        [
            new("event", "string", true, EventSelectorDescription),
            new("to", "string", false, TargetFramesDescription),
        ]),
        new("delete-frame-event", "Deletes an event; later events of the frame shift down by one.",
        [
            new("event", "string", true, EventSelectorDescription),
        ]),
        new("set-frame-keyframe", "Turns a frame's offset or color keyframe on or off. A chain's first frame is always a keyframe. Frames that are not keyframes glide between their neighbours.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("group", "string", true, "Which values glide.", FrameGroupValues),
            new("value", "boolean", true, "true for a keyframe."),
        ]),
        new("set-frame-keyframe-curve", "Sets the curve from a frame's offset or color keyframe to the next one. The frame must be a keyframe of that group.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("group", "string", true, "Which values glide.", FrameGroupValues),
            new("curve", "string", true, "The curve.", CurveValues),
        ]),
        new("set-shape-keyframe", "Turns a shape's offset, size or points keyframe on or off. The first shape of a run of frames holding its name is always a keyframe. Size is for rect and circle, points for polygon.",
        [
            new("shape", "string", true, ShapeAddressDescription),
            new("group", "string", true, "Which values glide.", ShapeGroupValues),
            new("value", "boolean", true, "true for a keyframe."),
        ]),
        new("set-shape-keyframe-curve", "Sets the curve from a shape's offset, size or points keyframe to the next one. The shape must be a keyframe of that group.",
        [
            new("shape", "string", true, ShapeAddressDescription),
            new("group", "string", true, "Which values glide.", ShapeGroupValues),
            new("curve", "string", true, "The curve.", CurveValues),
        ]),
        new("reorder-shape", "Moves a shape so it ends up at the given position among the shapes of its own type in the frame. Files store shapes grouped by type, so a shape never crosses types.",
        [
            new("shape", "string", true, ShapeAddressDescription),
            new("index", "integer", true, "Final zero-based position among shapes of that type, 0 to count - 1."),
        ]),
        new("clear-frame-color", "Clears color parts back to inherited from earlier frames (set-frame-color can only set). Give at least one part as true.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("red", "boolean", false, "true to clear red."),
            new("green", "boolean", false, "true to clear green."),
            new("blue", "boolean", false, "true to clear blue."),
            new("alpha", "boolean", false, "true to clear alpha."),
            new("operation", "boolean", false, "true to clear the color operation."),
        ]),
        new("associate-tileset", "Records a Tiled .tsx as a sync target of this .achx/.achj (the editor's Associate Tiled Tileset): the batch's save then pushes the file's animations onto that tileset, and so does every later save. Writes the companion .tiledsync file; not undoable. A .tsx that is an association target cannot be opened as a native project. Not for a .tsx.",
        [
            new("path", "string", true, "Tileset path, relative to the animation file or absolute."),
        ]),
        new("set-chain-owner-tile", "Native .tsx only: chooses which tile carries a chain's animation in the Tiled file. Give tile, or clear to return to the tile of the chain's first frame. A tile past the tileset or owned by another chain fails.",
        [
            new("chain", "string", true, "Chain name (a glob works with clear)."),
            new("tile", "integer", false, "Zero-based tile id."),
            new("clear", "boolean", false, "true to return to the first frame's tile."),
        ]),
        new("set-unit", "Changes the file's time unit and converts every frame length (locked chains too) so playback time is unchanged. Later ops in the batch use the new unit.",
        [
            new("unit", "string", true, "The new time unit.", UnitValues),
        ]),
    ];

    private const string CopyDescription =
        "What to do when the texture is outside the animation file's folder: keep references it in place (default), copy copies it next to the file, overwrite copies over a different same-named file.";

    /// <summary>A minimal valid one-line ops file, shown first in <c>--schema</c> and in the wrong-shape error.</summary>
    public const string ExampleFile = "{\"ops\":[{\"op\":\"rename-chain\",\"chain\":\"Walk\",\"to\":\"Run\"}]}";

    /// <summary>Validates the text of an ops file: JSON shape, known ops, known fields, field types. Nothing is applied.</summary>
    public static OpsParse Parse(string json) => Parse(json, Specs);

    /// <summary>Validates an ops file against <paramref name="specs"/>, so the same parser serves every file kind.</summary>
    public static OpsParse Parse(string json, IReadOnlyList<OpSpec> specs)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { return Bad($"The ops file is not valid JSON: {ex.Message}"); }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("ops", out var array)
                || array.ValueKind != JsonValueKind.Array)
                return Bad($"The ops file must be an object with an \"ops\" array, not a bare array. Example: {ExampleFile}");

            var ops = new List<EditOp>();
            int index = 0;
            foreach (var element in array.EnumerateArray())
            {
                var parsed = ParseOp(index, element, specs);
                if (parsed.Error is not null) return Bad(parsed.Error);
                ops.Add(parsed.Op!);
                index++;
            }
            return new OpsParse(ops, null);
        }
    }

    private static (EditOp? Op, string? Error) ParseOp(int index, JsonElement element, IReadOnlyList<OpSpec> specs)
    {
        string At(string message) => $"op {index}: {message}";

        if (element.ValueKind != JsonValueKind.Object) return (null, At("must be an object."));
        if (!element.TryGetProperty("op", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            return (null, At("needs a string \"op\" field."));

        var name = nameElement.GetString()!;
        var spec = specs.FirstOrDefault(s => s.Name == name);
        if (spec is null)
            return (null, At($"unknown op '{name}'." + OpHint(name, specs)));

        var values = new Dictionary<string, string>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == "op") continue;
            var field = spec.Fields.FirstOrDefault(f => f.Name == property.Name);
            if (field is null) return (null, At($"'{name}' has no field '{property.Name}'." + NameHints.For(property.Name, spec.Fields.Select(f => f.Name), "fields")));
            var typed = ReadValue(field, property.Value);
            if (typed is null)
                return (null, At($"field '{field.Name}' of '{name}' must be {FieldTypeText(field, property.Value)}."));
            var value = typed;
            if (field.Values is not null && !field.Values.Contains(value))
                return (null, At($"field '{field.Name}' of '{name}' must be one of {string.Join("|", field.Values)}, not '{value}'."));
            values[field.Name] = value;
        }

        var missing = spec.Fields.FirstOrDefault(f => f.Required && !values.ContainsKey(f.Name));
        if (missing is not null) return (null, At($"'{name}' needs the field '{missing.Name}'."));

        return (new EditOp(index, spec, values), null);
    }

    /// <summary>The field's value as text (numbers in invariant culture), or null when the JSON type does not match the field.</summary>
    private static string? ReadValue(OpField field, JsonElement value)
    {
        switch (field.Type)
        {
            case "integer" or "number" when value.ValueKind == JsonValueKind.String:
                return field.Relative ? ReadNumberText(field, value.GetString()!) : null;
            case "integer":
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)
                    ? i.ToString(CultureInfo.InvariantCulture) : null;
            case "boolean":
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? (value.GetBoolean() ? "true" : "false") : null;
            case "number":
                return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) && double.IsFinite(d)
                    ? d.ToString("R", CultureInfo.InvariantCulture) : null;
            case "points":
                return ReadPoints(value);
            default:
                return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }

    /// <summary>A relative value as typed, or an absolute one evaluated; null when <paramref name="text"/> is neither.</summary>
    private static string? ReadNumberText(OpField field, string text)
    {
        if (!NumericEdit.TryParse(text, out var edit)) return null;
        if (edit.IsRelative) return text;
        var value = edit.Apply(0m);
        if (field.Type == "integer" && (value != decimal.Truncate(value) || value is < int.MinValue or > int.MaxValue)) return null;
        return value.ToString(field.Type == "integer" ? "0" : null, CultureInfo.InvariantCulture);
    }

    /// <summary>A JSON array of [x, y] number pairs as "x,y;x,y;..." (invariant culture), or null when it is not one.</summary>
    private static string? ReadPoints(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return null;
        var parts = new List<string>();
        foreach (var pair in value.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2) return null;
            var coords = new List<string>();
            foreach (var n in pair.EnumerateArray())
            {
                if (n.ValueKind != JsonValueKind.Number || !n.TryGetDouble(out var d) || !double.IsFinite(d)) return null;
                coords.Add(d.ToString("R", CultureInfo.InvariantCulture));
            }
            parts.Add($"{coords[0]},{coords[1]}");
        }
        return string.Join(";", parts);
    }

    private static string FieldTypeText(OpField field, JsonElement value)
    {
        var relative = value.ValueKind == JsonValueKind.String && NumericEdit.TryParse(value.GetString(), out var edit) && edit.IsRelative;
        var a = field.Type == "integer" ? "an" : "a";
        return field.Type switch
        {
            "integer" or "number" when field.Relative => $"{a} {field.Type}, or a relative value such as +2",
            "integer" or "number" when relative => $"an absolute {field.Type}; '{value.GetString()}' is relative",
            "points" => "a list of absolute [x, y] number pairs, as [[x,y],[x,y],...]",
            _ => $"{a} {field.Type}",
        };
    }

    /// <summary>The nearest op name, or the whole list when nothing is close.</summary>
    private static string OpHint(string name, IReadOnlyList<OpSpec> specs)
    {
        var hint = NameHints.For(name, specs.Select(s => s.Name), "ops");
        return hint.Contains("Did you mean") ? hint : $" Known ops: {string.Join(", ", specs.Select(s => s.Name))}.";
    }

    private static OpsParse Bad(string error) => new(null, error);

    /// <summary>
    /// Applies <paramref name="ops"/> to <paramref name="session"/>'s document in order and, only if every op
    /// succeeded and no check error code has more errors than before, saves once. Otherwise nothing is written, and a texture an op
    /// already copied is put back as it was. An empty or no-change batch writes nothing either, so the file stays byte-identical.
    /// </summary>
    public static async Task<EditRunResult> ApplyAsync(EditSession session, IReadOnlyList<EditOp> ops)
    {
        EditRunResult run;
        try
        {
            run = await ApplyAllAsync(session, ops);
        }
        catch
        {
            session.Rollback.Restore();
            throw;
        }
        var notRestored = session.Rollback.Restore();
        return notRestored.Count == 0 ? run : run with { Result = run.Result with { Warnings = [.. run.Result.Warnings, .. notRestored] } };
    }

    private static async Task<EditRunResult> ApplyAllAsync(EditSession session, IReadOnlyList<EditOp> ops)
    {
        var before = CheckReport.Run(session.Document, session.Path);
        var changed = new List<string>();
        var warnings = new List<string>();
        if (!before.Ok)
            warnings.Add($"The file already had {before.Errors.Count()} check error(s) before the edit.");

        var tsxBefore = session.IsNativeTsx ? TsxOpPolicy.Take(session) : null;
        var associations = new List<string>();
        var chainsBefore = BrokenSequenceBlocks.ChainsAtRisk(session.Document, ops);

        foreach (var op in ops)
        {
            // No file or animation name can hold one, and the path functions throw on it.
            if (op.Values.FirstOrDefault(v => v.Value.Contains('\0')).Key is { } field)
                return FailedOp(warnings, op, "", Fail($"{field} cannot contain a null character.", EditErrorCode.InvalidValue));

            var refused = TsxOpPolicy.Check(session, op);
            if (refused is not null)
                return new EditRunResult(
                    new EditResult([], [$"op {op.Index} ({op.Spec.Name}): {refused.Errors[0]}"], warnings, refused.ErrorCodes),
                    [op.Index]);

            if (op.Spec.Name == "associate-tileset")
            {
                // Writes a companion file, so it waits until every other op and check has passed.
                var tsxPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(session.Path) ?? string.Empty, op.Values["path"]));
                var check = TsxEdit.CheckAssociation(session.ProjectManager, tsxPath);
                if (!check.Success)
                    return new EditRunResult(
                        new EditResult([], [$"op {op.Index} ({op.Spec.Name}): {check.Errors[0]}"], warnings, check.ErrorCodes),
                        [op.Index]);
                associations.Add(tsxPath);
                changed.Add(tsxPath);
                continue;
            }

            var expansion = SelectorExpander.Expand(session.Document, op);
            if (expansion.Ops is null)
                return new EditRunResult(
                    new EditResult([], [$"op {op.Index} ({op.Spec.Name}): {expansion.Error}"], warnings, [expansion.Code]),
                    [op.Index]);

            EditRunResult TargetFailed(EditResult result, string? target) =>
                FailedOp(warnings, op, target is null ? "" : $"target {target}: ", result);

            // Every target first: changing one can change what a later one inherits.
            var targets = new List<ExpandedOp>();
            foreach (var target in expansion.Ops)
            {
                var resolved = NumberFields.ResolveRelative(target.Op, field => CurrentValue(session.Document, target.Op, field));
                if (resolved.Error is not null) return TargetFailed(resolved.Error, target.Target);
                targets.Add(target with { Op = resolved.Op });
            }

            foreach (var target in targets)
            {
                var result = await ApplyOneAsync(session, target.Op);
                if (!result.Success) return TargetFailed(result, target.Target);
                changed.AddRange(result.ChangedItems);
            }
        }

        var after = CheckReport.Run(session.Document, session.Path);
        var added = CheckReport.NewErrors(before, after);
        if (added.Count > 0)
            return new EditRunResult(
                new EditResult([], added.Select(i => $"The edit would raise the count of check errors [{i.Code}]: {i.Message}").ToList(), warnings,
                added.Select(_ => EditErrorCode.WouldBreakFile).ToList()),
                added.Select(_ => (int?)null).ToList());

        if (tsxBefore is not null && TsxOpPolicy.CheckWritable(session, tsxBefore) is { } unwritable)
            return new EditRunResult(
                new EditResult([], unwritable.Errors, warnings, unwritable.ErrorCodes),
                unwritable.Errors.Select(_ => (int?)null).ToList());

        if (changed.Count == 0)
            return new EditRunResult(new EditResult([], [], warnings), []);

        foreach (var tsxPath in associations)
        {
            try { session.Rollback.Remember(Path.ChangeExtension(session.Path, ".tiledsync")); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // in use: the association reports it
            var associated = TsxEdit.AssociateTileset(session.Commands, session.ProjectManager, tsxPath);
            if (!associated.Success)
                return new EditRunResult(
                    new EditResult([], associated.Errors, warnings, associated.ErrorCodes),
                    associated.Errors.Select(_ => (int?)null).ToList());
        }

        var save = session.Save();
        if (!save.Success)
            return new EditRunResult(new EditResult([], save.Errors, warnings, save.ErrorCodes), save.Errors.Select(_ => (int?)null).ToList());
        session.Rollback.Forget();

        // A .tsx save leaves out what a tileset cannot hold (a chain with no frames), so the document says nothing about the saved file.
        if (!session.IsNativeTsx) warnings.AddRange(CheckReport.RaisedWarnings(before, after));
        if (chainsBefore is not null)
            warnings.AddRange(BrokenSequenceBlocks.Warnings(session.Document, session.Path, chainsBefore));
        var written = changed.Concat(save.ChangedItems.Where(p => p != session.Path)).Distinct().ToList();
        return new EditRunResult(new EditResult(written, [], warnings.Concat(save.Warnings).ToList()), []);
    }

    /// <summary>The run's result when an op failed: each error prefixed with the op and <paramref name="where"/>.</summary>
    internal static EditRunResult FailedOp(List<string> warnings, EditOp op, string where, EditResult result)
    {
        var errors = result.Errors.Select(e => $"op {op.Index} ({op.Spec.Name}): {where}{e}").ToList();
        return new EditRunResult(new EditResult([], errors, warnings, result.ErrorCodes), errors.Select(_ => (int?)op.Index).ToList());
    }

    /// <summary>What a relative value of <paramref name="field"/> starts from, or why the op's target cannot be resolved.</summary>
    private static (decimal Value, EditResult? Error) CurrentValue(AnimationChainListSave doc, EditOp op, OpField field)
    {
        decimal value = 0;
        EditResult Take(float current)
        {
            value = (decimal)current;
            return new EditResult([], [], []);
        }

        EditResult Color(AnimationChainSave chain, AnimationFrameSave frame)
        {
            var inherited = EffectiveFrameColor.Resolve(chain.Frames, chain.Frames.IndexOf(frame));
            int unset = EffectiveFrameColor.ChannelDefault(inherited.Operation);
            return field.Name switch
            {
                "red" => Take(frame.Red ?? inherited.Red ?? unset),
                "green" => Take(frame.Green ?? inherited.Green ?? unset),
                "blue" => Take(frame.Blue ?? inherited.Blue ?? unset),
                _ => Take(frame.Alpha ?? inherited.Alpha ?? 255),
            };
        }

        var found = op.Spec.Name switch
        {
            "set-chain-length" => WithChain(doc, op, chain => Take(chain.Frames.Sum(f => f.FrameLength))),
            "set-frame-length" => WithFrame(doc, op, (_, frame) => Take(frame.FrameLength)),
            "set-frame-offset" => WithFrame(doc, op, (_, frame) => Take(field.Name == "x" ? frame.RelativeX : frame.RelativeY)),
            "set-frame-color" => WithFrame(doc, op, Color),
            _ => field.Name switch
            {
                "x" => WithShape<ShapeSave>(doc, op, "shape", shape => Take(shape.X)),
                "y" => WithShape<ShapeSave>(doc, op, "shape", shape => Take(shape.Y)),
                "scaleX" => WithShape<AARectSave>(doc, op, "rect", rect => Take(rect.ScaleX)),
                "scaleY" => WithShape<AARectSave>(doc, op, "rect", rect => Take(rect.ScaleY)),
                "radius" => WithShape<CircleSave>(doc, op, "circle", circle => Take(circle.Radius)),
                _ => throw new InvalidOperationException($"No current value for '{field.Name}' of '{op.Spec.Name}'."),
            },
        };
        return (value, found.Success ? null : found);
    }

    private static Task<EditResult> ApplyOneAsync(EditSession session, EditOp op)
    {
        var doc = session.Document;
        switch (op.Spec.Name)
        {
            case "rename-chain":
            {
                var chain = EditAddress.ResolveChain(doc, op.Values["chain"]);
                return Task.FromResult(chain.Ok
                    ? RenameEdit.RenameChain(session.ProjectManager, session.Commands, chain.Value!, op.Values["to"])
                    : Fail(chain.Error!, chain.Code));
            }
            case "rename-shape":
            {
                var shape = EditAddress.ResolveShape(doc, op.Values["shape"]);
                return Task.FromResult(shape.Ok
                    ? RenameEdit.RenameShape(session.Commands, session.ObjectFinder, shape.Value!, op.Values["to"])
                    : Fail(shape.Error!, shape.Code));
            }
            case "set-frame-texture":
                return Task.FromResult(SetFrameTexture(session, op));
            case "add-chain":
                return Task.FromResult(StructureEdit.AddChain(session.ProjectManager, session.Commands, op.Values["name"]));
            case "delete-chain":
                return Task.FromResult(WithChain(doc, op, c => StructureEdit.DeleteChain(session.ProjectManager, session.Commands, c)));
            case "duplicate-chain":
                return Task.FromResult(WithChain(doc, op, c =>
                    StructureEdit.DuplicateChain(session.ProjectManager, session.Commands, c, op.Values.GetValueOrDefault("name"))));
            case "import-chain":
                return Task.FromResult(StructureEdit.ImportChains(
                    session.ProjectManager, session.Commands,
                    Path.GetFullPath(Path.Combine(Path.GetDirectoryName(session.Path) ?? string.Empty, op.Values["from"])),
                    op.Values["chain"], op.Values.GetValueOrDefault("name")));
            case "reorder-chain":
                return Task.FromResult(WithChain(doc, op, c =>
                    StructureEdit.ReorderChain(session.ProjectManager, session.Commands, c, int.Parse(op.Values["index"], CultureInfo.InvariantCulture))));
            case "add-frame":
                return Task.FromResult(AddFrame(session, op));
            case "add-frames-grid":
                return Task.FromResult(AddFramesGrid(session, op));
            case "delete-frame":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) => StructureEdit.DeleteFrame(session.Commands, chain, frame)));
            case "duplicate-frame" when op.Values.ContainsKey("to") || op.Values.ContainsKey("index"):
                return Task.FromResult(DuplicateFramesTo(session, op));
            case "duplicate-frame":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) => StructureEdit.DuplicateFrame(session.Commands, chain, frame)));
            case "reorder-frame":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) =>
                    StructureEdit.ReorderFrame(session.Commands, chain, frame, int.Parse(op.Values["index"], CultureInfo.InvariantCulture))));
            case "set-chain-loop":
                return Task.FromResult(WithChain(doc, op, c => PropertyEdit.SetChainLoop(session.Commands, c, Flag(op, "value"))));
            case "set-chain-pingpong":
                return Task.FromResult(WithChain(doc, op, c => PropertyEdit.SetChainPingPong(session.Commands, c, Flag(op, "value"))));
            case "set-chain-reverse":
                return Task.FromResult(WithChain(doc, op, c => PropertyEdit.SetChainReverse(session.Commands, c, Flag(op, "value"))));
            case "set-chain-locked":
                return Task.FromResult(WithChain(doc, op, c => PropertyEdit.SetChainLocked(session.Commands, c, Flag(op, "value"))));
            case "set-chain-length":
                return Task.FromResult(WithChain(doc, op, c => PropertyEdit.SetChainLength(
                    session.ProjectManager, session.Commands, c, Number(op, "length")!.Value, op.Values.GetValueOrDefault("mode") == "even")));
            case "invert-frame-order":
                return Task.FromResult(WithChain(doc, op, c => StructureEdit.InvertFrameOrder(session.Commands, c)));
            case "justify-offsets-bottom":
                return Task.FromResult(WithChain(doc, op, c => PropertyEdit.JustifyOffsetsBottom(session.Commands, c)));
            case "set-frame-length":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) =>
                    PropertyEdit.SetFrameLength(session.ProjectManager, session.Commands, chain, frame, Number(op, "length")!.Value)));
            case "set-frame-region":
                return Task.FromResult(SetFrameRegion(session, op));
            case "set-frame-offset":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) =>
                    PropertyEdit.SetFrameOffset(session.Commands, chain, frame, Number(op, "x"), Number(op, "y"))));
            case "set-frame-color":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) => PropertyEdit.SetFrameColor(session.Commands, chain, frame,
                    new FrameColorEdit(Integer(op, "red"), Integer(op, "green"), Integer(op, "blue"), Integer(op, "alpha"),
                        op.Values.GetValueOrDefault("operation") switch { "multiply" => ColorOperation.Multiply, "add" => ColorOperation.Add, _ => null }))));
            case "set-frame-flip":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) =>
                    PropertyEdit.SetFrameFlip(session.Commands, chain, frame, OptionalFlag(op, "flipH"), OptionalFlag(op, "flipV"), OptionalFlag(op, "flipDiagonal"))));
            case "add-shape":
                return Task.FromResult(WithFrame(doc, op, (_, frame) =>
                    ShapeEdit.AddShape(session.Commands, session.ObjectFinder, frame, op.Values["type"], op.Values.GetValueOrDefault("name"))));
            case "delete-shape":
                return Task.FromResult(WithShape<ShapeSave>(doc, op, "shape", shape => ShapeEdit.DeleteShape(session.Commands, session.ObjectFinder, shape)));
            case "duplicate-shape" when op.Values.ContainsKey("to"):
                return Task.FromResult(WithShape<ShapeSave>(doc, op, "shape", shape =>
                    WithTargetFrames(doc, op, targets => ShapeEdit.DuplicateShapeTo(session.Commands, shape, targets!))));
            case "duplicate-shape":
                return Task.FromResult(WithShape<ShapeSave>(doc, op, "shape", shape => ShapeEdit.DuplicateShape(session.Commands, session.ObjectFinder, shape)));
            case "set-rect":
                return Task.FromResult(WithShape<AARectSave>(doc, op, "rect", rect =>
                    ShapeEdit.SetRect(session.Commands, session.ObjectFinder, rect, Number(op, "x"), Number(op, "y"), Number(op, "scaleX"), Number(op, "scaleY"))));
            case "match-frame-size":
                return Task.FromResult(WithShape<AARectSave>(doc, op, "rect", rect =>
                    ShapeEdit.MatchFrameSize(session.Commands, session.ObjectFinder, rect)));
            case "set-circle":
                return Task.FromResult(WithShape<CircleSave>(doc, op, "circle", circle =>
                    ShapeEdit.SetCircle(session.Commands, session.ObjectFinder, circle, Number(op, "x"), Number(op, "y"), Number(op, "radius"))));
            case "set-polygon":
                return Task.FromResult(WithShape<PolygonSave>(doc, op, "polygon", polygon =>
                    ShapeEdit.SetPolygon(session.Commands, session.ObjectFinder, polygon, Number(op, "x"), Number(op, "y"), Points(op))));
            case "set-shape-common":
                return Task.FromResult(WithShape<ShapeSave>(doc, op, "shape", shape =>
                    ShapeEdit.SetCommon(session.Commands, session.ObjectFinder, shape, Number(op, "x"), Number(op, "y"))));
            case "add-frame-event":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) =>
                    FrameDetailEdit.AddFrameEvent(session.Commands, chain, frame, op.Values["name"], op.Values.GetValueOrDefault("data"))));
            case "set-frame-event":
                return Task.FromResult(WithEvent(doc, op, e => FrameDetailEdit.SetFrameEvent(
                    session.Commands, e.Chain, e.Frame, e.Index, op.Values.GetValueOrDefault("name"), op.Values.GetValueOrDefault("data"))));
            case "duplicate-frame-event":
                return Task.FromResult(WithEvent(doc, op, e =>
                    WithTargetFrames(doc, op, targets => FrameDetailEdit.DuplicateFrameEvent(session.Commands, e, targets))));
            case "delete-frame-event":
                return Task.FromResult(WithEvent(doc, op, e => FrameDetailEdit.DeleteFrameEvent(session.Commands, e.Chain, e.Frame, e.Index)));
            case "set-frame-keyframe":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) => FrameDetailEdit.SetFrameKeyframe(
                    session.Commands, chain, frame, op.Values["group"] == "offset" ? KeyframeGroup.Offset : KeyframeGroup.Color, Flag(op, "value"))));
            case "set-frame-keyframe-curve":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) => FrameDetailEdit.SetFrameKeyframeCurve(
                    session.Commands, chain, frame, op.Values["group"] == "offset" ? KeyframeGroup.Offset : KeyframeGroup.Color, Curve(op))));
            case "set-shape-keyframe":
                return Task.FromResult(WithShape<ShapeSave>(doc, op, "shape", shape => FrameDetailEdit.SetShapeKeyframe(
                    session.Commands, session.ObjectFinder, shape, ShapeGroup(op), Flag(op, "value"))));
            case "set-shape-keyframe-curve":
                return Task.FromResult(WithShape<ShapeSave>(doc, op, "shape", shape => FrameDetailEdit.SetShapeKeyframeCurve(
                    session.Commands, session.ObjectFinder, shape, ShapeGroup(op), Curve(op))));
            case "reorder-shape":
                return Task.FromResult(WithShape<ShapeSave>(doc, op, "shape", shape => FrameDetailEdit.ReorderShape(
                    session.Commands, session.ObjectFinder, shape, int.Parse(op.Values["index"], CultureInfo.InvariantCulture))));
            case "clear-frame-color":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) => FrameDetailEdit.ClearFrameColor(
                    session.Commands, chain, frame,
                    new FrameColorClear(OptionalFlag(op, "red") == true, OptionalFlag(op, "green") == true, OptionalFlag(op, "blue") == true,
                        OptionalFlag(op, "alpha") == true, OptionalFlag(op, "operation") == true))));
            case "set-chain-owner-tile":
                return Task.FromResult(WithChain(doc, op, c => SetOwnerTile(session, op, c)));
            case "set-unit":
                return Task.FromResult(PropertyEdit.SetTimeUnit(session.ProjectManager, session.Commands,
                    op.Values["unit"] == "millisecond" ? TimeMeasurementUnit.Millisecond : TimeMeasurementUnit.Second));
            default:
                return Task.FromResult(Fail($"Unhandled op '{op.Spec.Name}'.", EditErrorCode.InvalidValue));
        }
    }

    private static EditResult SetOwnerTile(EditSession session, EditOp op, AnimationChainSave chain)
    {
        var hasTile = op.Values.ContainsKey("tile");
        var clear = OptionalFlag(op, "clear") == true;
        if (hasTile == clear)
            return Fail("set-chain-owner-tile needs exactly one of tile and clear.", EditErrorCode.InvalidValue);
        if (hasTile && Integer(op, "tile")!.Value < 0)
            return Fail("The tile id cannot be negative.", EditErrorCode.InvalidValue);

        return hasTile
            ? TsxEdit.SetChainOwnerTile(session.ProjectManager, session.Commands, chain, (uint)Integer(op, "tile")!.Value)
            : TsxEdit.ClearChainOwnerTile(session.ProjectManager, session.Commands, chain);
    }

    private static EditResult SetFrameTexture(EditSession session, EditOp op) => WithFrame(session.Document, op, (chain, frame) =>
    {
        // Before the texture is resolved, since that may copy the file.
        if (chain.IsLocked) return StructureEdit.LockedFail(chain);

        var resolved = ResolveTexture(session, op);
        return resolved.AbsolutePath is null
            ? resolved.Result
            : FrameTextureEdit.SetResolved(session.ProjectManager, session.Commands, [frame], resolved.AbsolutePath, new HeaderTextureProbe());
    });

    /// <summary>The op's texture as an absolute path inside the project, after applying its <c>copy</c> choice.</summary>
    private static TextureResolveResult ResolveTexture(EditSession session, EditOp op)
    {
        var folder = Path.GetDirectoryName(session.Path) ?? string.Empty;
        var absolute = TexturePathHelper.ResolveDisplayPath(op.Values["texture"], folder);
        if (!File.Exists(absolute))
            return new TextureResolveResult(null, Fail($"Could not load image: {absolute}", EditErrorCode.TextureNotFound));

        var choice = (op.Values.GetValueOrDefault("copy") ?? "keep") switch
        {
            "copy" => TextureCopyChoice.Copy,
            "overwrite" => TextureCopyChoice.Overwrite,
            _ => TextureCopyChoice.KeepInPlace,
        };
        try
        {
            // Copied now, since this op and later ones read the copy's size; a failed batch undoes it.
            var plan = TextureCopyDecider.PlanCopyForProject(session.ProjectManager, absolute);
            if (plan is null) return new TextureResolveResult(absolute, new EditResult([], [], []));
            if (choice is not TextureCopyChoice.KeepInPlace) session.Rollback.Remember(plan.DestinationPath);
            return FrameTextureEdit.ApplyChoice(plan, choice);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new TextureResolveResult(null, Fail($"Could not copy: {ex.Message}", EditErrorCode.TextureCopyFailed));
        }
    }

    private static EditResult AddFrame(EditSession session, EditOp op)
    {
        var chain = EditAddress.ResolveChain(session.Document, op.Values["chain"]);
        if (!chain.Ok) return Fail(chain.Error!, chain.Code);

        FramePixelRegion? region = null;
        if (op.Values.TryGetValue("region", out var text))
        {
            var parsed = ParseRegion(text);
            if (parsed.Region is null) return Fail(parsed.Error!, EditErrorCode.InvalidValue);
            region = parsed.Region;
        }

        float? length = op.Values.TryGetValue("length", out var lengthText)
            ? float.Parse(lengthText, CultureInfo.InvariantCulture)
            : null;

        var resolved = ResolveTexture(session, op);
        if (resolved.AbsolutePath is null) return resolved.Result;

        return StructureEdit.AddFrame(
            session.ProjectManager, session.Commands, chain.Value!, resolved.AbsolutePath, region, length, new HeaderTextureProbe());
    }

    private static EditResult AddFramesGrid(EditSession session, EditOp op)
    {
        var chain = EditAddress.ResolveChain(session.Document, op.Values["chain"]);
        if (!chain.Ok) return Fail(chain.Error!, chain.Code);

        var resolved = ResolveTexture(session, op);
        if (resolved.AbsolutePath is null) return resolved.Result;

        return StructureEdit.AddFramesGrid(
            session.ProjectManager, session.Commands, chain.Value!, resolved.AbsolutePath,
            Integer(op, "cellWidth")!.Value, Integer(op, "cellHeight")!.Value, Integer(op, "columns")!.Value, Integer(op, "rows")!.Value,
            Integer(op, "start") ?? 0, Integer(op, "count"), Number(op, "frameLength"), new HeaderTextureProbe());
    }

    private static EditResult DuplicateFramesTo(EditSession session, EditOp op)
    {
        var sources = SelectorExpander.ResolveFrames(session.Document, op.Values["frame"]);
        if (sources.Frames is null) return Fail(sources.Error!, sources.Code);

        AnimationChainSave? to = null;
        if (op.Values.TryGetValue("to", out var name))
        {
            var chain = EditAddress.ResolveChain(session.Document, name);
            if (!chain.Ok) return Fail(chain.Error!, chain.Code);
            to = chain.Value;
        }
        return StructureEdit.DuplicateFramesTo(session.Commands, sources.Frames, to, Integer(op, "index"));
    }

    /// <summary>Runs <paramref name="apply"/> with the frames the op's <c>to</c> names, or null when it has none.</summary>
    private static EditResult WithTargetFrames(AnimationChainListSave doc, EditOp op, Func<IReadOnlyList<FrameRef>?, EditResult> apply)
    {
        if (!op.Values.TryGetValue("to", out var address)) return apply(null);
        var targets = SelectorExpander.ResolveFrames(doc, address);
        return targets.Frames is null ? Fail(targets.Error!, targets.Code) : apply(targets.Frames);
    }

    private static EditResult SetFrameRegion(EditSession session, EditOp op)
    {
        var parsed = ParseRegion(op.Values["region"]);
        if (parsed.Region is null) return Fail(parsed.Error!, EditErrorCode.InvalidValue);
        return WithFrame(session.Document, op, (chain, frame) =>
            PropertyEdit.SetFrameRegion(session.ProjectManager, session.Commands, chain, frame, parsed.Region.Value));
    }

    private static (FramePixelRegion? Region, string? Error) ParseRegion(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && !p.Trim().StartsWith('+')))
            return (null, $"The region '{text}' must be four whole pixel numbers: x,y,width,height, all absolute.");
        var n = parts.Select(p => int.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
        return (new FramePixelRegion(n[0], n[1], n[2], n[3]), null);
    }

    private static bool Flag(EditOp op, string field) => op.Values[field] == "true";

    private static bool? OptionalFlag(EditOp op, string field) => op.Values.TryGetValue(field, out var v) ? v == "true" : null;

    private static float? Number(EditOp op, string field) =>
        op.Values.TryGetValue(field, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : null;

    private static int? Integer(EditOp op, string field) =>
        op.Values.TryGetValue(field, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : null;

    private static IReadOnlyList<(float X, float Y)>? Points(EditOp op) =>
        op.Values.TryGetValue("points", out var text)
            ? text.Length == 0
                ? []
                : text.Split(';').Select(pair =>
                {
                    var xy = pair.Split(',');
                    return (float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture));
                }).ToList()
            : null;

    /// <summary>Resolves the op's shape address and checks it is a <typeparamref name="T"/> (<paramref name="typeWord"/> names it in the message).</summary>
    private static EditResult WithShape<T>(AnimationChainListSave doc, EditOp op, string typeWord, Func<T, EditResult> apply)
        where T : ShapeSave
    {
        var shape = EditAddress.ResolveShape(doc, op.Values["shape"]);
        if (!shape.Ok) return Fail(shape.Error!, shape.Code);
        return shape.Value is T typed
            ? apply(typed)
            : Fail($"'{op.Values["shape"]}' is a {EditAddress.TypeName(shape.Value!)}, but {op.Spec.Name} needs a {typeWord}.", EditErrorCode.WrongShapeType);
    }

    private static KeyframeCurveChoice Curve(EditOp op) => op.Values["curve"] switch
    {
        "snap" => KeyframeCurveChoice.Snap,
        "linear" => KeyframeCurveChoice.Linear,
        "ease-in" => KeyframeCurveChoice.EaseIn,
        "ease-out" => KeyframeCurveChoice.EaseOut,
        "ease-in-out" => KeyframeCurveChoice.EaseInOut,
        "anticipate" => KeyframeCurveChoice.Anticipate,
        "overshoot" => KeyframeCurveChoice.Overshoot,
        _ => KeyframeCurveChoice.Bounce,
    };

    private static ShapeKeyframeGroup ShapeGroup(EditOp op) => op.Values["group"] switch
    {
        "offset" => ShapeKeyframeGroup.Offset,
        "size" => ShapeKeyframeGroup.Size,
        _ => ShapeKeyframeGroup.Points,
    };

    private static EditResult WithEvent(AnimationChainListSave doc, EditOp op, Func<FrameEventRef, EditResult> apply)
    {
        var e = EditAddress.ResolveEvent(doc, op.Values["event"]);
        return e.Ok ? apply(e.Value!) : Fail(e.Error!, e.Code);
    }

    private static EditResult WithChain(AnimationChainListSave doc, EditOp op, Func<AnimationChainSave, EditResult> apply)
    {
        var chain = EditAddress.ResolveChain(doc, op.Values["chain"]);
        return chain.Ok ? apply(chain.Value!) : Fail(chain.Error!, chain.Code);
    }

    private static EditResult WithFrame(AnimationChainListSave doc, EditOp op, Func<AnimationChainSave, AnimationFrameSave, EditResult> apply)
    {
        var frame = EditAddress.ResolveFrame(doc, op.Values["frame"]);
        if (!frame.Ok) return Fail(frame.Error!, frame.Code);
        var chain = EditAddress.ResolveChain(doc, op.Values["frame"].Split('/')[0]);
        return chain.Ok ? apply(chain.Value!, frame.Value!) : Fail(chain.Error!, chain.Code);
    }

    private static EditResult Fail(string message, EditErrorCode code = EditErrorCode.Unspecified) =>
        new([], [message], [], [code]);
}
