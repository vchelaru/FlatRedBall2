using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;

namespace FlatRedBall2.IO;

/// <summary>
/// A parsed CSV, addressable by header name.
/// </summary>
/// <remarks>
/// Deliberately a general reader rather than a Glue-specific one — a game with its own data tables
/// wants the same thing. It handles the dialect FlatRedBall's tooling produces: typed headers such
/// as <c>MaxSpeed (float)</c>, one column marked <c>required</c> to act as a key, <c>#</c> comments,
/// and rows commented out with a leading <c>//</c>.
/// <para>Values stay as text. The caller knows what it wants them to be, and a header's declared
/// type is frequently not a CLR type at all.</para>
/// </remarks>
public sealed class CsvTable
{
    private CsvTable(IReadOnlyList<CsvHeader> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    /// <summary>The parsed header row.</summary>
    public IReadOnlyList<CsvHeader> Headers { get; }

    /// <summary>Data rows, each the same length as <see cref="Headers"/>.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }

    /// <summary>The header marked <c>required</c>, whose column is the natural key. Null if none.</summary>
    public CsvHeader? KeyHeader
    {
        get
        {
            foreach (var header in Headers)
            {
                if (header.IsRequired)
                    return header;
            }

            return null;
        }
    }

    /// <summary>Parses CSV text.</summary>
    public static CsvTable Parse(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        List<CsvHeader>? headers = null;

        foreach (var fields in ReadRecords(text))
        {
            if (headers is null)
            {
                headers = new List<CsvHeader>(fields.Count);

                foreach (string field in fields)
                    headers.Add(CsvHeader.Parse(field));

                continue;
            }

            // A row whose first cell is commented out, or that is entirely empty, is not data.
            if (fields.Count == 0 || fields[0].StartsWith("//", StringComparison.Ordinal))
                continue;

            bool allEmpty = true;

            foreach (string field in fields)
            {
                if (field.Length > 0)
                {
                    allEmpty = false;
                    break;
                }
            }

            if (allEmpty)
                continue;

            // Rows are fixed-width against the header: extra cells are dropped, missing ones read
            // as empty, so a caller can index by header position without bounds checks.
            var row = new string[headers.Count];

            for (int i = 0; i < headers.Count; i++)
                row[i] = i < fields.Count ? fields[i] : string.Empty;

            rows.Add(row);
        }

        return new CsvTable(
            (IReadOnlyList<CsvHeader>?)headers ?? Array.Empty<CsvHeader>(), rows);
    }

    /// <summary>The value in <paramref name="row"/> under <paramref name="headerName"/>, or null.</summary>
    public string? Value(IReadOnlyList<string> row, string headerName)
    {
        for (int i = 0; i < Headers.Count && i < row.Count; i++)
        {
            if (string.Equals(Headers[i].Name, headerName, StringComparison.OrdinalIgnoreCase))
                return row[i];
        }

        return null;
    }

    /// <summary>Reads a float, returning <paramref name="fallback"/> when absent or unparseable.</summary>
    public float Float(IReadOnlyList<string> row, string headerName, float fallback = 0f) =>
        float.TryParse(Value(row, headerName), NumberStyles.Float, CultureInfo.InvariantCulture,
            out float value)
            ? value
            : fallback;

    /// <summary>Reads a bool, returning <paramref name="fallback"/> when absent or unparseable.</summary>
    public bool Bool(IReadOnlyList<string> row, string headerName, bool fallback = false) =>
        bool.TryParse(Value(row, headerName), out bool value) ? value : fallback;

    /// <summary>
    /// Resolves every header to a <see cref="CsvColumn"/> — its parsed name/type-text/required flag
    /// plus the CLR type that text resolved to. This is the schema step: it parses headers and
    /// resolves types, and nothing else, so a future consumer (e.g. generating a real class per CSV
    /// instead of a runtime <see cref="CsvRow"/>) can reuse it without writing its own header parser
    /// or type resolver. <see cref="ToDictionary"/> is one such consumer, not the only reason this
    /// exists.
    /// </summary>
    /// <param name="onUnresolvedType">
    /// Called once per column whose declared type is neither a C# builtin nor found by reflection
    /// against currently loaded assemblies.
    /// </param>
    public IReadOnlyList<CsvColumn> ResolveSchema(Action<CsvHeader>? onUnresolvedType = null)
    {
        var columns = new CsvColumn[Headers.Count];

        for (int i = 0; i < Headers.Count; i++)
        {
            Type? type = ResolveColumnType(Headers[i].Type);

            if (type is null && Headers[i].Type.Length > 0)
                onUnresolvedType?.Invoke(Headers[i]);

            columns[i] = new CsvColumn(Headers[i], type);
        }

        return columns;
    }

    /// <summary>
    /// Converts every data row into a <see cref="CsvRow"/>, keyed by <see cref="KeyHeader"/>'s
    /// value, using <see cref="ResolveSchema"/> to convert each cell to the CLR type its column
    /// declares — a C# builtin (<c>bool</c>, <c>int</c>, <c>float?</c>, ...), or, for anything else,
    /// a type resolved by name against every currently loaded assembly. That is how a header like
    /// <c>WeaponUpgradeType (MyGame.Types.WeaponUpgradeType)</c> reaches a caller's own enum without
    /// this library ever referencing it at compile time.
    /// </summary>
    /// <param name="onUnresolvedType">
    /// Called once per column whose declared type is neither a builtin nor found by reflection.
    /// That column's cells fall back to raw, unconverted text rather than being dropped.
    /// </param>
    /// <returns>Rows keyed by <see cref="KeyHeader"/>'s value. Empty when there is no required column.</returns>
    public Dictionary<string, CsvRow> ToDictionary(Action<CsvHeader>? onUnresolvedType = null)
    {
        var result = new Dictionary<string, CsvRow>(StringComparer.OrdinalIgnoreCase);
        var key = KeyHeader;

        if (key is null)
            return result;

        var columns = ResolveSchema(onUnresolvedType);

        foreach (var row in Rows)
        {
            string keyValue = Value(row, key.Value.Name) ?? string.Empty;

            if (keyValue.Length == 0)
                continue;

            var values = new Dictionary<string, object?>(columns.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < columns.Count && i < row.Count; i++)
                values[columns[i].Header.Name] = ConvertCell(row[i], columns[i].Type);

            result[keyValue] = new CsvRow(values);
        }

        return result;
    }

    /// <summary>
    /// Resolves a header's declared type text to a CLR type. An empty declaration (no parenthesized
    /// type at all) is treated as <see cref="string"/> rather than unresolved — most CSVs have at
    /// least one untyped column, and that is not the caller's own type Glue failed to find.
    /// </summary>
    /// <remarks>
    /// Looking up a type by name is inherently trim/AOT-unsafe — the type is only known at CSV-load
    /// time, so a trimmer has no reference to preserve it by. That is an accepted limitation of
    /// resolving a caller's own type from data rather than a bug: a game publishing trimmed must
    /// keep any type it names in a header, e.g. with a <c>DynamicDependency</c> attribute.
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Deliberately dynamic: a header names a type only the caller's own " +
            "assembly knows about, so there is nothing to reference statically.")]
    private static Type? ResolveColumnType(string typeText)
    {
        if (typeText.Length == 0)
            return typeof(string);

        string bare = typeText[^1] == '?' ? typeText[..^1] : typeText;

        Type? builtin = bare switch
        {
            "string" or "String" or "System.String" => typeof(string),
            "bool" or "Boolean" or "System.Boolean" => typeof(bool),
            "int" or "Int32" or "System.Int32" => typeof(int),
            "long" or "Int64" or "System.Int64" => typeof(long),
            "float" or "Single" or "System.Single" => typeof(float),
            "double" or "Double" or "System.Double" => typeof(double),
            "decimal" or "Decimal" or "System.Decimal" => typeof(decimal),
            _ => null,
        };

        if (builtin is not null)
            return builtin;

        // Not a builtin — the type belongs to the caller's own compiled assembly, which this
        // library cannot reference. Resolve it by name against everything already loaded instead.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var found = assembly.GetType(bare, throwOnError: false, ignoreCase: false);

            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Converts one cell's raw text to <paramref name="type"/>. An empty cell is always <c>null</c>
    /// — a nullable column's absence and a non-nullable column's absence are the same "no value"
    /// here, since a CSV cell being blank does not distinguish the two. A cell that fails to parse
    /// (the header's declared type disagreeing with what was actually written) keeps its raw text
    /// rather than being dropped, matching the behavior for an unresolved column type.
    /// </summary>
    private static object? ConvertCell(string rawText, Type? type)
    {
        if (rawText.Length == 0)
            return null;

        if (type is null || type == typeof(string))
            return rawText;

        if (type.IsEnum)
            return Enum.TryParse(type, rawText, ignoreCase: true, out object? enumValue)
                ? enumValue
                : rawText;

        if (type == typeof(bool))
            return bool.TryParse(rawText, out bool b) ? b : rawText;

        if (type == typeof(int))
            return int.TryParse(rawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                ? i
                : rawText;

        if (type == typeof(long))
            return long.TryParse(rawText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)
                ? l
                : rawText;

        if (type == typeof(float))
            return float.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
                ? f
                : rawText;

        if (type == typeof(double))
            return double.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                ? d
                : rawText;

        if (type == typeof(decimal))
            return decimal.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal m)
                ? m
                : rawText;

        return rawText;
    }

    /// <summary>Splits text into records, honouring quotes, doubled-quote escapes and comments.</summary>
    private static IEnumerable<List<string>> ReadRecords(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool any = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c != '"')
                {
                    field.Append(c);
                    continue;
                }

                // A doubled quote inside a quoted field is an escaped quote, not the end of it.
                if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    any = true;
                    break;

                case ',':
                    fields.Add(field.ToString().Trim());
                    field.Clear();
                    any = true;
                    break;

                case '#' when field.Length == 0 && fields.Count == 0:
                    // A whole-line comment: skip to the end of it.
                    while (i < text.Length && text[i] != '\n')
                        i++;

                    break;

                case '\r':
                    break;

                case '\n':
                    fields.Add(field.ToString().Trim());
                    field.Clear();

                    if (any || fields.Count > 1 || fields[0].Length > 0)
                        yield return new List<string>(fields);

                    fields.Clear();
                    any = false;
                    break;

                default:
                    field.Append(c);
                    any = true;
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString().Trim());
            yield return fields;
        }
    }
}

/// <summary>
/// One CSV column: its member name, its declared type text, and whether it is the key.
/// </summary>
/// <remarks>
/// FlatRedBall's tooling writes headers as <c>Name (type)</c>, optionally with <c>, required</c>.
/// The type and the required marker can appear in either order, and both spellings of the type text
/// occur in real files — <c>string</c> from one generator and <c>System.String</c> from another.
/// </remarks>
public readonly struct CsvHeader
{
    private CsvHeader(string name, string type, bool isRequired, string originalText)
    {
        Name = name;
        Type = type;
        IsRequired = isRequired;
        OriginalText = originalText;
    }

    /// <summary>The member name, with whitespace removed — <c>Max HP (int)</c> becomes <c>MaxHP</c>.</summary>
    public string Name { get; }

    /// <summary>The declared type text, as written.</summary>
    public string Type { get; }

    /// <summary>Whether this column is marked as the required key.</summary>
    public bool IsRequired { get; }

    /// <summary>The header exactly as it appeared.</summary>
    public string OriginalText { get; }

    /// <summary>Parses one header cell.</summary>
    public static CsvHeader Parse(string text)
    {
        string trimmed = text.Trim().Trim('"');
        int open = trimmed.IndexOf('(');

        if (open < 0)
            return new CsvHeader(StripWhitespace(trimmed), string.Empty, false, text);

        // Whitespace goes before the truncation, so "Max HP (int)" yields "MaxHP".
        string name = StripWhitespace(trimmed[..open]);

        int close = trimmed.LastIndexOf(')');
        string inside = close > open ? trimmed[(open + 1)..close] : trimmed[(open + 1)..];

        bool isRequired = false;
        string type = string.Empty;

        foreach (string part in inside.Split(','))
        {
            string piece = part.Trim();

            if (piece.Equals("required", StringComparison.OrdinalIgnoreCase))
                isRequired = true;
            else if (type.Length == 0)
                type = piece;
        }

        return new CsvHeader(name, type, isRequired, text);
    }

    private static string StripWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            if (!char.IsWhiteSpace(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => OriginalText;
}

/// <summary>
/// One column's header alongside the CLR type its declared type text resolved to. The output of
/// <see cref="CsvTable.ResolveSchema"/> — the schema step shared by every consumer that needs a
/// column's name, declared type, and required-ness, whether that consumer builds a runtime
/// <see cref="CsvRow"/> or (in the future) generates a real class.
/// </summary>
public readonly struct CsvColumn
{
    internal CsvColumn(CsvHeader header, Type? type)
    {
        Header = header;
        Type = type;
    }

    /// <summary>The column's parsed header — name, declared type text, and whether it is the key.</summary>
    public CsvHeader Header { get; }

    /// <summary>
    /// The CLR type <see cref="Header"/>'s declared type text resolved to, or <c>null</c> when it
    /// is neither a builtin nor found by reflection.
    /// </summary>
    public Type? Type { get; }
}
