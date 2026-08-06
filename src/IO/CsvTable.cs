using System;
using System.Collections.Generic;
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
