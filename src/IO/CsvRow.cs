using System.Collections.Generic;

namespace FlatRedBall2.IO;

/// <summary>
/// One row of a <see cref="CsvTable.ToDictionary"/> result, with each column already converted to
/// the CLR type its header declared.
/// </summary>
/// <remarks>
/// Not a generated class — nothing here is codegen'd per CSV. A column whose declared type could not
/// be resolved keeps its raw text instead, so <see cref="Get{T}"/> with <c>string</c> still works
/// even when a header names a type this library has never heard of.
/// </remarks>
public sealed class CsvRow
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    internal CsvRow(IReadOnlyDictionary<string, object?> values) => _values = values;

    /// <summary>The column's converted value, untyped, or null when absent or empty.</summary>
    public object? this[string columnName] =>
        _values.TryGetValue(columnName, out object? value) ? value : null;

    /// <summary>
    /// The column's value as <typeparamref name="T"/>. Returns <c>default</c> when the column is
    /// absent, its cell was empty, or the stored value does not match <typeparamref name="T"/> —
    /// which happens when a header's declared type disagreed with what a cell actually held.
    /// </summary>
    public T? Get<T>(string columnName) => this[columnName] is T typed ? typed : default;
}
