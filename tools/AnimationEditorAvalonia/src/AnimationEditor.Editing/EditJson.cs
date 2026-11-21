using System.Text.Json;

namespace AnimationEditor.Editing;

/// <summary>Compact JSON renderings of results, shared by every command that has a <c>--json</c> mode.</summary>
public static class EditJson
{
    /// <summary>Renders what <paramref name="write"/> writes as one compact JSON document.</summary>
    public static string Render(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
            write(w);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// <c>{"success","changed":[],"errors":[{"op"?,"code","message"}],"warnings":[]}</c>. <paramref name="errorOps"/>
    /// is parallel to the errors and may be null.
    /// </summary>
    public static void WriteResult(Utf8JsonWriter w, EditResult result, IReadOnlyList<int?>? errorOps = null)
    {
        w.WriteStartObject();
        w.WriteBoolean("success", result.Success);
        w.WriteStartArray("changed");
        foreach (var c in result.ChangedItems) w.WriteStringValue(c);
        w.WriteEndArray();
        w.WriteStartArray("errors");
        for (int i = 0; i < result.Errors.Count; i++)
        {
            w.WriteStartObject();
            if (errorOps is not null && i < errorOps.Count && errorOps[i] is int op) w.WriteNumber("op", op);
            w.WriteString("code", result.CodeAt(i).ToString());
            w.WriteString("message", result.Errors[i]);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("warnings");
        foreach (var warning in result.Warnings) w.WriteStringValue(warning);
        w.WriteEndArray();
        w.WriteEndObject();
    }

    public static void WriteCheck(Utf8JsonWriter w, CheckReport report)
    {
        w.WriteStartObject();
        w.WriteBoolean("ok", report.Ok);
        w.WriteStartArray("issues");
        foreach (var issue in report.Issues)
        {
            w.WriteStartObject();
            w.WriteString("severity", issue.Severity == CheckSeverity.Error ? "error" : "warning");
            w.WriteString("code", issue.Code);
            w.WriteString("message", issue.Message);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    public static void WriteSchema(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        w.WriteString("format", "{\"ops\":[{\"op\":\"<name>\", ...fields}, ...]}");
        w.WriteString("selectors", EditOps.SelectorSyntax);
        w.WriteString("tsx", EditOps.TsxNote);
        w.WriteStartArray("ops");
        foreach (var spec in EditOps.Specs) WriteSpec(w, spec);
        w.WriteEndArray();
        w.WriteString("sequenceSelectors", SequenceOps.AddressSyntax);
        w.WriteStartArray("sequenceOps");
        foreach (var spec in SequenceOps.Specs) WriteSpec(w, spec);
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteSpec(Utf8JsonWriter w, OpSpec spec)
    {
        w.WriteStartObject();
        w.WriteString("op", spec.Name);
        w.WriteString("description", spec.Description);
        w.WriteStartArray("fields");
        foreach (var f in spec.Fields)
        {
            w.WriteStartObject();
            w.WriteString("name", f.Name);
            w.WriteString("type", f.Type);
            w.WriteBoolean("required", f.Required);
            w.WriteString("description", f.Description);
            if (f.Values is not null)
            {
                w.WriteStartArray("values");
                foreach (var v in f.Values) w.WriteStringValue(v);
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteEndObject();
    }

    public static string SchemaText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Animation files (.achx/.achj). Ops file: {\"ops\":[{\"op\":\"<name>\", ...fields}, ...]}, applied in order.");
        sb.AppendLine("Addresses: chain Walk, frame Walk/2 (zero-based), shape Walk/2/rect/Hitbox (rect|circle|polygon).");
        sb.AppendLine(EditOps.SelectorSyntax);
        sb.AppendLine(EditOps.TsxNote);
        AppendSpecs(sb, EditOps.Specs);
        sb.AppendLine();
        sb.AppendLine("Sequence files (.seqj) take these ops instead; the file extension picks the set.");
        sb.AppendLine(SequenceOps.AddressSyntax);
        AppendSpecs(sb, SequenceOps.Specs);
        return sb.ToString();
    }

    private static void AppendSpecs(System.Text.StringBuilder sb, IReadOnlyList<OpSpec> specs)
    {
        foreach (var spec in specs)
        {
            sb.AppendLine();
            sb.AppendLine($"{spec.Name}: {spec.Description}");
            foreach (var f in spec.Fields)
            {
                var values = f.Values is null ? "" : $" [{string.Join("|", f.Values)}]";
                sb.AppendLine($"  {f.Name}  {f.Type}{values}  {(f.Required ? "required" : "optional")}  {f.Description}");
            }
        }
    }
}
