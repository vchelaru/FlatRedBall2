using System.Text.Json;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <param name="Type">JSON type of the value; every field of this slice is a "string".</param>
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
            new("copy", "string", false,
                "What to do when the texture is outside the animation file's folder: keep references it in place (default), copy copies it next to the file, overwrite copies over a different same-named file.",
                CopyValues),
        ]),
    ];

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
            if (property.Value.ValueKind != JsonValueKind.String)
                return (null, At($"field '{field.Name}' of '{name}' must be a string."));
            var value = property.Value.GetString()!;
            if (field.Values is not null && !field.Values.Contains(value))
                return (null, At($"field '{field.Name}' of '{name}' must be one of {string.Join("|", field.Values)}, not '{value}'."));
            values[field.Name] = value;
        }

        var missing = spec.Fields.FirstOrDefault(f => f.Required && !values.ContainsKey(f.Name));
        if (missing is not null) return (null, At($"'{name}' needs the field '{missing.Name}'."));

        return (new EditOp(index, spec, values), null);
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
            default:
                return Task.FromResult(Fail($"Unhandled op '{op.Spec.Name}'."));
        }
    }

    private static Task<EditResult> SetFrameTextureAsync(EditSession session, EditOp op)
    {
        var frame = EditAddress.ResolveFrame(session.Document, op.Values["frame"]);
        if (!frame.Ok) return Task.FromResult(Fail(frame.Error!));

        var folder = Path.GetDirectoryName(session.Path) ?? string.Empty;
        var absolute = TexturePathHelper.ResolveDisplayPath(op.Values["texture"], folder);
        if (!File.Exists(absolute))
            return Task.FromResult(Fail($"Could not load image: {absolute}", EditErrorCode.TextureNotFound));

        var choice = (op.Values.GetValueOrDefault("copy") ?? "keep") switch
        {
            "copy" => TextureCopyChoice.Copy,
            "overwrite" => TextureCopyChoice.Overwrite,
            _ => TextureCopyChoice.KeepInPlace,
        };
        var resolved = FrameTextureEdit.ResolveForProject(session.ProjectManager, absolute, choice);
        if (resolved.AbsolutePath is null) return Task.FromResult(resolved.Result);

        return Task.FromResult(FrameTextureEdit.SetResolved(
            session.ProjectManager, session.Commands, [frame.Value!], resolved.AbsolutePath, new HeaderTextureProbe()));
    }

    private static EditResult Fail(string message, EditErrorCode code = EditErrorCode.Unspecified) =>
        new([], [message], [], [code]);
}
