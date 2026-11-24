using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AnimationEditor.Editing;

/// <summary>
/// One-line form of an op for <c>edit --op</c> (#1907): <c>&lt;op-name&gt; &lt;primary address&gt; key=value ...</c>. Driven entirely by the
/// <see cref="OpSpec"/>s, so every op (and every future op) has it: the primary address is the op's first required field, written
/// without a key (<c>key=value</c> also works for it); everything else is <c>key=value</c>. Each inline op is turned into the
/// same JSON an ops file holds and handed to <see cref="EditOps.Parse(string, IReadOnlyList{OpSpec})"/>, so validation and error
/// text are the ops file's, plus the expected form of the failing op.
/// </summary>
public static class InlineOps
{
    /// <summary>How <c>--schema</c> explains the inline form; shared by the text and JSON output.</summary>
    public const string Syntax =
        "Inline form (edit <file> --op \"<op-name> <primary> key=value ...\", repeatable, applied in order; edit <file> --ops - reads the ops file JSON from stdin): "
        + "the primary address is the op's first required field, written without its name (key=value also works). "
        + "Values: numbers (-3, 0.5), true/false, text (quote it when it has spaces: name=\"Walk Cycle\" or 'Walk Cycle'), "
        + "and for points a JSON array (points=[[0,0],[8,0],[8,8]]). A text value is always taken literally. Each op shows its form below as 'inline:'.";

    /// <summary>The expected inline form of an op, e.g. <c>set-frame-offset &lt;frame&gt; [x=&lt;number&gt;] [y=&lt;number&gt;]</c>.</summary>
    public static string Form(OpSpec spec)
    {
        var sb = new StringBuilder(spec.Name);
        var primary = Primary(spec);
        foreach (var f in spec.Fields)
        {
            sb.Append(' ');
            var text = f == primary ? $"<{f.Name}>" : $"{f.Name}=<{FieldHint(f)}>";
            sb.Append(f.Required ? text : $"[{text}]");
        }
        return sb.ToString();
    }

    private static string FieldHint(OpField f) => f.Values is not null ? string.Join("|", f.Values) : f.Type == "points" ? "[[x,y],...]" : f.Type;

    private static OpField? Primary(OpSpec spec) => spec.Fields.FirstOrDefault(f => f.Required);

    /// <summary>Parses the inline ops (in order) against <paramref name="specs"/>; nothing is applied.</summary>
    public static OpsParse Parse(IReadOnlyList<string> lines, IReadOnlyList<OpSpec> specs)
    {
        var ops = new JsonArray();
        for (int i = 0; i < lines.Count; i++)
        {
            var built = Build(i, lines[i], specs);
            if (built.Error is not null) return new OpsParse(null, built.Error);
            ops.Add(built.Op);
        }

        var parsed = EditOps.Parse(new JsonObject { ["ops"] = ops }.ToJsonString(), specs);
        if (parsed.Error is null) return parsed;

        // Re-explain a validation failure of a known op with that op's expected form.
        var m = Regex.Match(parsed.Error, @"^op (\d+):");
        if (m.Success && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) is var index
            && index < lines.Count && specs.FirstOrDefault(s => s.Name == OpName(lines[index])) is { } spec)
            return new OpsParse(null, $"{parsed.Error} Expected: {Form(spec)}");
        return parsed;
    }

    private static string? OpName(string line) => Tokenize(line).Tokens is [{ } first, ..] ? first.Text : null;

    private static (JsonObject? Op, string? Error) Build(int index, string line, IReadOnlyList<OpSpec> specs)
    {
        string At(string message) => $"op {index}: {message}";

        var (tokens, tokenError) = Tokenize(line);
        if (tokenError is not null) return (null, At(tokenError));
        if (tokens.Count == 0) return (null, At("is empty. Expected: <op-name> <primary address> key=value ..."));

        var name = tokens[0].Text;
        var op = new JsonObject { ["op"] = name };
        var spec = specs.FirstOrDefault(s => s.Name == name);
        if (spec is null) return (op, null); // EditOps.Parse reports the unknown op with its did-you-mean

        var primary = Primary(spec);
        bool primaryGiven = false;
        foreach (var token in tokens.Skip(1))
        {
            OpField? field;
            string valueText;
            if (token.Key is not null && spec.Fields.FirstOrDefault(f => f.Name == token.Key) is { } named)
            {
                field = named;
                valueText = token.Value;
            }
            else if (token.Key is not null && Regex.IsMatch(token.Key, @"^[A-Za-z_]\w*$"))
            {
                // An unknown field name: pass it on as text so the shared validation names it with a hint.
                op[token.Key] = token.Value;
                continue;
            }
            else if (primary is not null && !primaryGiven && !op.ContainsKey(primary.Name))
            {
                field = primary;
                valueText = token.Text;
                primaryGiven = true;
            }
            else
            {
                return (null, At($"unexpected '{token.Text}'. Expected: {Form(spec)}"));
            }

            if (op.ContainsKey(field.Name)) return (null, At($"field '{field.Name}' given twice. Expected: {Form(spec)}"));
            var (node, error) = ToNode(field, valueText);
            if (error is not null) return (null, At($"{error} Expected: {Form(spec)}"));
            op[field.Name] = node;
        }
        return (op, null);
    }

    /// <summary>The JSON value of a field from its inline text. Wrong-typed text is passed on as a string so the shared validation reports it.</summary>
    private static (JsonNode? Node, string? Error) ToNode(OpField field, string text)
    {
        switch (field.Type)
        {
            case "integer" or "number":
                return (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d)
                    ? JsonValue.Create(d) : JsonValue.Create(text), null);
            case "boolean":
                return (text is "true" or "false" ? JsonValue.Create(text == "true") : JsonValue.Create(text), null);
            case "points":
                try { return (JsonNode.Parse(text), null); }
                catch (JsonException ex) { return (null, $"field '{field.Name}' is not valid JSON ({ex.Message.Split('.')[0]})."); }
            default:
                return (JsonValue.Create(text), null);
        }
    }

    private sealed record Token(string Text, string? Key, string Value);

    /// <summary>
    /// Splits on whitespace. Single or double quotes group (and are dropped); a [ or { opens a JSON value that runs, quotes
    /// and all, to its matching bracket. A token is key=value when it has an unquoted '=' after a bare key.
    /// </summary>
    private static (List<Token> Tokens, string? Error) Tokenize(string line)
    {
        var tokens = new List<Token>();
        var text = new StringBuilder();
        int eq = -1, depth = 0;
        char quote = '\0';
        bool inToken = false, jsonString = false;

        void Flush()
        {
            if (!inToken) return;
            var all = text.ToString();
            tokens.Add(eq > 0 ? new Token(all, all[..eq], all[(eq + 1)..]) : new Token(all, null, all));
            text.Clear();
            eq = -1;
            inToken = false;
        }

        foreach (var c in line)
        {
            if (depth > 0)
            {
                text.Append(c);
                if (c == '"') jsonString = !jsonString;
                else if (!jsonString && c is '[' or '{') depth++;
                else if (!jsonString && c is ']' or '}') depth--;
                continue;
            }
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                else text.Append(c);
                continue;
            }
            if (char.IsWhiteSpace(c)) { Flush(); continue; }

            inToken = true;
            if (c is '"' or '\'') quote = c;
            else if (c is '[' or '{') { depth = 1; text.Append(c); }
            else
            {
                if (c == '=' && eq < 0 && text.Length > 0) eq = text.Length;
                text.Append(c);
            }
        }
        if (quote != '\0') return (tokens, $"has an unclosed {quote} quote.");
        if (depth > 0) return (tokens, "has an unclosed [ or { value.");
        Flush();
        return (tokens, null);
    }
}
