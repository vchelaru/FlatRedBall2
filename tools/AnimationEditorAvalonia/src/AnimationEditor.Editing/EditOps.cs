using System.Globalization;
using System.Text.Json;
using AnimationEditor.Core.IO;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <param name="Type">JSON type of the value: "string", "integer" or "number".</param>
/// <param name="Values">The allowed values when the field is an enumeration.</param>
public sealed record OpField(string Name, string Type, bool Required, string Description, IReadOnlyList<string>? Values = null);

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
            new("chain", "string", true, "Chain name."),
        ]),
        new("duplicate-chain", "Inserts a copy of a chain right after it, frames and shapes included.",
        [
            new("chain", "string", true, "Chain name."),
            new("name", "string", false, "Name of the copy, unique in the file. Default: the source name with a number added (Walk becomes Walk2)."),
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
        new("delete-frame", "Deletes one frame.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
        ]),
        new("duplicate-frame", "Inserts a copy of a frame right after it, shapes included.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
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
        new("set-frame-length", "Sets one frame's length in the file's own time unit (see describe). Valid range: 0.001 to 60 seconds, or 1 to 60000 milliseconds.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("length", "number", true, "New length in the file's time unit."),
        ]),
        new("set-frame-region", "Sets the pixel rectangle of the texture a frame shows. The frame's texture must be an existing PNG and the rectangle must lie inside it.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("region", "string", true, RegionDescription),
        ]),
        new("set-frame-offset", "Sets a frame's relative offset. Give x, y or both; an omitted axis is left as it is.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("x", "number", false, "Relative X."),
            new("y", "number", false, "Relative Y."),
        ]),
        new("set-frame-color", "Sets a frame's color channels and operation. Give at least one; omitted ones are left as they are. A channel cannot be cleared back to inherited.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("red", "integer", false, "Red, -255 to 255."),
            new("green", "integer", false, "Green, -255 to 255."),
            new("blue", "integer", false, "Blue, -255 to 255."),
            new("alpha", "integer", false, "Alpha, 0 to 255."),
            new("operation", "string", false, "How red, green and blue combine with the texture.", ColorOperationValues),
        ]),
        new("set-frame-flip", "Sets a frame's flip flags to the given values (not a toggle). Give at least one; omitted ones are left as they are. A change also mirrors the frame's offset and shapes, as in the editor.",
        [
            new("frame", "string", true, "Frame address: Chain/frameIndex."),
            new("flipH", "boolean", false, "Flip horizontally."),
            new("flipV", "boolean", false, "Flip vertically."),
            new("flipDiagonal", "boolean", false, "Flip along the diagonal."),
        ]),
        new("set-unit", "Changes the file's time unit and converts every frame length (locked chains too) so playback time is unchanged. Later ops in the batch use the new unit.",
        [
            new("unit", "string", true, "The new time unit.", UnitValues),
        ]),
    ];

    private const string CopyDescription =
        "What to do when the texture is outside the animation file's folder: keep references it in place (default), copy copies it next to the file, overwrite copies over a different same-named file.";

    /// <summary>Validates the text of an ops file: JSON shape, known ops, known fields, field types. Nothing is applied.</summary>
    public static OpsParse Parse(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { return Bad($"The ops file is not valid JSON: {ex.Message}"); }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("ops", out var array)
                || array.ValueKind != JsonValueKind.Array)
                return Bad("The ops file must be an object with an \"ops\" array.");

            var ops = new List<EditOp>();
            int index = 0;
            foreach (var element in array.EnumerateArray())
            {
                var parsed = ParseOp(index, element);
                if (parsed.Error is not null) return Bad(parsed.Error);
                ops.Add(parsed.Op!);
                index++;
            }
            return new OpsParse(ops, null);
        }
    }

    private static (EditOp? Op, string? Error) ParseOp(int index, JsonElement element)
    {
        string At(string message) => $"op {index}: {message}";

        if (element.ValueKind != JsonValueKind.Object) return (null, At("must be an object."));
        if (!element.TryGetProperty("op", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            return (null, At("needs a string \"op\" field."));

        var name = nameElement.GetString()!;
        var spec = Specs.FirstOrDefault(s => s.Name == name);
        if (spec is null)
            return (null, At($"unknown op '{name}'. Known ops: {string.Join(", ", Specs.Select(s => s.Name))}."));

        var values = new Dictionary<string, string>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name == "op") continue;
            var field = spec.Fields.FirstOrDefault(f => f.Name == property.Name);
            if (field is null) return (null, At($"'{name}' has no field '{property.Name}'."));
            var typed = ReadValue(field, property.Value);
            if (typed is null)
                return (null, At($"field '{field.Name}' of '{name}' must be {(field.Type == "integer" ? "an integer" : $"a {field.Type}")}."));
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
            case "integer":
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)
                    ? i.ToString(CultureInfo.InvariantCulture) : null;
            case "boolean":
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? (value.GetBoolean() ? "true" : "false") : null;
            case "number":
                return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) && double.IsFinite(d)
                    ? d.ToString("R", CultureInfo.InvariantCulture) : null;
            default:
                return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }

    private static OpsParse Bad(string error) => new(null, error);

    /// <summary>
    /// Applies <paramref name="ops"/> to <paramref name="session"/>'s document in order and, only if every op
    /// succeeded and no new check error appeared, saves once. Otherwise nothing is written. An empty or no-change
    /// batch writes nothing either, so the file stays byte-identical.
    /// </summary>
    public static async Task<EditRunResult> ApplyAsync(EditSession session, IReadOnlyList<EditOp> ops)
    {
        var before = CheckReport.Run(session.Document, session.Path);
        var changed = new List<string>();
        var warnings = new List<string>();
        if (!before.Ok)
            warnings.Add($"The file already had {before.Errors.Count()} check error(s) before the edit.");

        foreach (var op in ops)
        {
            var result = await ApplyOneAsync(session, op);
            if (!result.Success)
            {
                var errors = result.Errors.Select(e => $"op {op.Index} ({op.Spec.Name}): {e}").ToList();
                return new EditRunResult(
                    new EditResult([], errors, warnings, result.ErrorCodes),
                    errors.Select(_ => (int?)op.Index).ToList());
            }
            changed.AddRange(result.ChangedItems);
        }

        var added = CheckReport.NewErrors(before, CheckReport.Run(session.Document, session.Path));
        if (added.Count > 0)
            return new EditRunResult(
                new EditResult([], added.Select(i => $"The edit would introduce a check error: {i.Message}").ToList(), warnings),
                added.Select(_ => (int?)null).ToList());

        if (changed.Count == 0)
            return new EditRunResult(new EditResult([], [], warnings), []);

        var save = session.Save();
        return save.Success
            ? new EditRunResult(new EditResult(changed, [], warnings), [])
            : new EditRunResult(new EditResult([], save.Errors, warnings), save.Errors.Select(_ => (int?)null).ToList());
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
                    : Fail(chain.Error!));
            }
            case "rename-shape":
            {
                var shape = EditAddress.ResolveShape(doc, op.Values["shape"]);
                return Task.FromResult(shape.Ok
                    ? RenameEdit.RenameShape(session.Commands, session.ObjectFinder, shape.Value!, op.Values["to"])
                    : Fail(shape.Error!));
            }
            case "set-frame-texture":
                return SetFrameTextureAsync(session, op);
            case "add-chain":
                return Task.FromResult(StructureEdit.AddChain(session.ProjectManager, session.Commands, op.Values["name"]));
            case "delete-chain":
                return Task.FromResult(WithChain(doc, op, c => StructureEdit.DeleteChain(session.ProjectManager, session.Commands, c)));
            case "duplicate-chain":
                return Task.FromResult(WithChain(doc, op, c =>
                    StructureEdit.DuplicateChain(session.ProjectManager, session.Commands, c, op.Values.GetValueOrDefault("name"))));
            case "reorder-chain":
                return Task.FromResult(WithChain(doc, op, c =>
                    StructureEdit.ReorderChain(session.ProjectManager, session.Commands, c, int.Parse(op.Values["index"], CultureInfo.InvariantCulture))));
            case "add-frame":
                return Task.FromResult(AddFrame(session, op));
            case "delete-frame":
                return Task.FromResult(WithFrame(doc, op, (chain, frame) => StructureEdit.DeleteFrame(session.Commands, chain, frame)));
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
            case "set-unit":
                return Task.FromResult(PropertyEdit.SetTimeUnit(session.ProjectManager, session.Commands,
                    op.Values["unit"] == "millisecond" ? TimeMeasurementUnit.Millisecond : TimeMeasurementUnit.Second));
            default:
                return Task.FromResult(Fail($"Unhandled op '{op.Spec.Name}'."));
        }
    }

    private static Task<EditResult> SetFrameTextureAsync(EditSession session, EditOp op)
    {
        var frame = EditAddress.ResolveFrame(session.Document, op.Values["frame"]);
        if (!frame.Ok) return Task.FromResult(Fail(frame.Error!));

        var resolved = ResolveTexture(session, op);
        if (resolved.AbsolutePath is null) return Task.FromResult(resolved.Result);

        return Task.FromResult(FrameTextureEdit.SetResolved(
            session.ProjectManager, session.Commands, [frame.Value!], resolved.AbsolutePath, new HeaderTextureProbe()));
    }

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
        return FrameTextureEdit.ResolveForProject(session.ProjectManager, absolute, choice);
    }

    private static EditResult AddFrame(EditSession session, EditOp op)
    {
        var chain = EditAddress.ResolveChain(session.Document, op.Values["chain"]);
        if (!chain.Ok) return Fail(chain.Error!);

        FramePixelRegion? region = null;
        if (op.Values.TryGetValue("region", out var text))
        {
            var parsed = ParseRegion(text);
            if (parsed.Region is null) return Fail(parsed.Error!);
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

    private static EditResult SetFrameRegion(EditSession session, EditOp op)
    {
        var parsed = ParseRegion(op.Values["region"]);
        if (parsed.Region is null) return Fail(parsed.Error!);
        return WithFrame(session.Document, op, (chain, frame) =>
            PropertyEdit.SetFrameRegion(session.ProjectManager, session.Commands, chain, frame, parsed.Region.Value));
    }

    private static (FramePixelRegion? Region, string? Error) ParseRegion(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            return (null, $"The region '{text}' must be four whole pixel numbers: x,y,width,height.");
        var n = parts.Select(p => int.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
        return (new FramePixelRegion(n[0], n[1], n[2], n[3]), null);
    }

    private static bool Flag(EditOp op, string field) => op.Values[field] == "true";

    private static bool? OptionalFlag(EditOp op, string field) => op.Values.TryGetValue(field, out var v) ? v == "true" : null;

    private static float? Number(EditOp op, string field) =>
        op.Values.TryGetValue(field, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : null;

    private static int? Integer(EditOp op, string field) =>
        op.Values.TryGetValue(field, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : null;

    private static EditResult WithChain(AnimationChainListSave doc, EditOp op, Func<AnimationChainSave, EditResult> apply)
    {
        var chain = EditAddress.ResolveChain(doc, op.Values["chain"]);
        return chain.Ok ? apply(chain.Value!) : Fail(chain.Error!);
    }

    private static EditResult WithFrame(AnimationChainListSave doc, EditOp op, Func<AnimationChainSave, AnimationFrameSave, EditResult> apply)
    {
        var frame = EditAddress.ResolveFrame(doc, op.Values["frame"]);
        if (!frame.Ok) return Fail(frame.Error!);
        var chain = EditAddress.ResolveChain(doc, op.Values["frame"].Split('/')[0]);
        return chain.Ok ? apply(chain.Value!, frame.Value!) : Fail(chain.Error!);
    }

    private static EditResult Fail(string message, EditErrorCode code = EditErrorCode.Unspecified) =>
        new([], [message], [], [code]);
}
