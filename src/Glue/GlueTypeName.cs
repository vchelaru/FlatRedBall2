using System;
using System.Collections.Generic;

namespace FlatRedBall2.Glue;

/// <summary>
/// A parsed Glue type string. Glue's <c>SourceClassType</c> is not a flat CLR type name: it may
/// carry an unresolved generic placeholder, closed generics whose arguments are Glue element names
/// rather than types, or an element reference in backslash form standing where a type would.
/// Matching the whole string against a lookup table therefore cannot work.
/// </summary>
public sealed class GlueTypeName
{
    private static readonly IReadOnlyList<GlueTypeName> NoArguments = Array.Empty<GlueTypeName>();

    private GlueTypeName(string openTypeName, IReadOnlyList<GlueTypeName> typeArguments)
    {
        OpenTypeName = openTypeName;
        TypeArguments = typeArguments;
    }

    /// <summary>The type name with any generic argument list stripped off.</summary>
    public string OpenTypeName { get; }

    /// <summary>The generic arguments, themselves parsed. Empty for a non-generic name.</summary>
    public IReadOnlyList<GlueTypeName> TypeArguments { get; }

    /// <summary>
    /// Whether this names a Glue element rather than a type. Element references are written in
    /// backslash form (<c>Entities\Player</c>), which no CLR type name contains.
    /// </summary>
    public bool IsElementReference => OpenTypeName.Contains('\\');

    /// <summary>
    /// The element being referenced, or null when this is a type. Returned in its original
    /// backslash form, because that is what element <c>Name</c>s compare against.
    /// </summary>
    public string? ElementName => IsElementReference ? OpenTypeName : null;

    /// <summary>
    /// Reduces this name to the backslash form an element's <c>Name</c> would use, so it can be
    /// looked up against a project's elements.
    /// </summary>
    /// <remarks>
    /// Glue writes the same entity two ways depending on position: <c>Entities\Player</c> standing
    /// alone, but <c>Entities.Player</c> as a generic argument, where it is the generated C# class
    /// name. This normalizes both.
    /// <para>Deliberately structural, with no attempt to guess whether the result <em>is</em> an
    /// element — the caller decides that by looking it up. Guessing from a prefix would misread
    /// <c>FlatRedBall.Entities.CameraControllingEntity</c>, an engine type whose namespace contains
    /// "Entities", as a game entity. A name that is not an element simply matches nothing.</para>
    /// </remarks>
    public string ToElementNameCandidate() => OpenTypeName.Replace('.', '\\');

    /// <summary>
    /// Whether a generic argument is still the declaration's placeholder (<c>&lt;T&gt;</c>) rather
    /// than a real argument. Glue writes lists this way, so the element type has to come from
    /// elsewhere on the object.
    /// </summary>
    public bool IsUnresolvedGeneric
    {
        get
        {
            for (int i = 0; i < TypeArguments.Count; i++)
            {
                if (TypeArguments[i].OpenTypeName is "T")
                    return true;
            }

            return false;
        }
    }

    /// <summary>Parses a Glue type string. Never throws — an unparseable string comes back as-is.</summary>
    public static GlueTypeName Parse(string? typeString)
    {
        if (string.IsNullOrWhiteSpace(typeString))
            return new GlueTypeName(string.Empty, NoArguments);

        string trimmed = typeString.Trim();
        int open = trimmed.IndexOf('<');

        if (open < 0 || !trimmed.EndsWith('>'))
            return new GlueTypeName(trimmed, NoArguments);

        string openName = trimmed[..open].Trim();
        string arguments = trimmed[(open + 1)..^1];

        return new GlueTypeName(openName, SplitArguments(arguments));
    }

    /// <summary>
    /// Splits a generic argument list on top-level commas only. A nested generic contains its own
    /// commas, so tracking depth is what keeps <c>Outer&lt;Inner&lt;A, B&gt;, C&gt;</c> from
    /// splitting into three arguments instead of two.
    /// </summary>
    private static IReadOnlyList<GlueTypeName> SplitArguments(string arguments)
    {
        var parsed = new List<GlueTypeName>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < arguments.Length; i++)
        {
            char character = arguments[i];

            if (character == '<')
            {
                depth++;
            }
            else if (character == '>')
            {
                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                parsed.Add(Parse(arguments[start..i]));
                start = i + 1;
            }
        }

        parsed.Add(Parse(arguments[start..]));
        return parsed;
    }

    /// <inheritdoc />
    public override string ToString() =>
        TypeArguments.Count == 0
            ? OpenTypeName
            : $"{OpenTypeName}<{string.Join(", ", TypeArguments)}>";
}
