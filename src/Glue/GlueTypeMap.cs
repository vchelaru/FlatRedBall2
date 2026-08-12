using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FlatRedBall2.Glue;

/// <summary>
/// Maps Glue's type strings onto FRB2 types.
/// </summary>
/// <remarks>
/// Deliberately small: it covers only the types this phase can build. Everything else — tile
/// collision, collision relationships, camera controllers, lists — is owned by a later phase and
/// reports as unmapped until that phase lands. An unmapped type is a diagnostic, never a failure.
/// </remarks>
public static class GlueTypeMap
{
    // Keyed on the open type name so a generic's arguments do not have to match for the outer type
    // to resolve. Adding a row is how a later phase claims a type.
    //
    // The value is a factory rather than a Type so construction is a direct `new`. Going through
    // Activator.CreateInstance(Type) instead is trim-unsafe: the constructor is not rooted, so a
    // trimmed or AOT publish throws MissingMethodException and every object comes back null — and
    // the IL2067 that warns about it only appears at publish time, never on `dotnet build`.
    private static readonly Dictionary<string, Func<object>> FactoriesByGlueName = new(StringComparer.Ordinal)
    {
        ["FlatRedBall.Sprite"] = static () => new Rendering.Sprite(),
        ["FlatRedBall.Math.Geometry.AxisAlignedRectangle"] = static () => new Collision.AARect(),
        ["FlatRedBall.Math.Geometry.Circle"] = static () => new Collision.Circle(),
        ["FlatRedBall.Math.Geometry.Polygon"] = static () => new Collision.Polygon(),
        ["FlatRedBall.Entities.CameraControllingEntity"] = static () => new Entities.CameraControllingEntity(),
    };

    private static readonly Dictionary<string, Type> TypesByGlueName = new(StringComparer.Ordinal)
    {
        ["FlatRedBall.Sprite"] = typeof(Rendering.Sprite),
        ["FlatRedBall.Math.Geometry.AxisAlignedRectangle"] = typeof(Collision.AARect),
        ["FlatRedBall.Math.Geometry.Circle"] = typeof(Collision.Circle),
        ["FlatRedBall.Math.Geometry.Polygon"] = typeof(Collision.Polygon),
        ["FlatRedBall.Entities.CameraControllingEntity"] = typeof(Entities.CameraControllingEntity),
    };

    /// <summary>
    /// Short spellings Glue writes instead of the fully-qualified name.
    /// </summary>
    /// <remarks>
    /// Older projects (`FileVersion` 54 and below) write <c>SourceClassType</c> unqualified, and
    /// <em>mix both spellings inside one file</em> — so this is not a version gate that could be
    /// applied per project. Aliasing in its own table rather than duplicating dictionary keys keeps
    /// one row per type as the unit by which a phase claims a type.
    /// <para>No ambiguity with element references: those contain a backslash and are rejected before
    /// this is consulted.</para>
    /// </remarks>
    private static readonly Dictionary<string, string> QualifiedByShortName = new(StringComparer.Ordinal)
    {
        ["Sprite"] = "FlatRedBall.Sprite",
        ["AxisAlignedRectangle"] = "FlatRedBall.Math.Geometry.AxisAlignedRectangle",
        ["Circle"] = "FlatRedBall.Math.Geometry.Circle",
        ["Polygon"] = "FlatRedBall.Math.Geometry.Polygon",
    };

    private static string Canonical(string openTypeName) =>
        QualifiedByShortName.TryGetValue(openTypeName, out string? qualified) ? qualified : openTypeName;

    /// <summary>Constructs an instance of the mapped type, without reflection.</summary>
    /// <returns>False if a later phase owns this type.</returns>
    public static bool TryCreate(GlueTypeName typeName, [NotNullWhen(true)] out object? instance)
    {
        instance = null;

        if (typeName.IsElementReference)
            return false;

        if (!FactoriesByGlueName.TryGetValue(Canonical(typeName.OpenTypeName), out var factory))
            return false;

        instance = factory();
        return true;
    }

    /// <summary>Resolves a parsed Glue type name to its FRB2 equivalent.</summary>
    /// <returns>True if this build can construct the type; false if a later phase owns it.</returns>
    public static bool TryGetType(GlueTypeName typeName, [NotNullWhen(true)] out Type? type)
    {
        type = null;

        // An element reference names a Screen or Entity in the project, not a CLR type.
        if (typeName.IsElementReference)
            return false;

        return TypesByGlueName.TryGetValue(Canonical(typeName.OpenTypeName), out type);
    }

    /// <summary>Resolves a raw Glue type string, parsing it first.</summary>
    public static bool TryGetType(string? glueTypeString, [NotNullWhen(true)] out Type? type) =>
        TryGetType(GlueTypeName.Parse(glueTypeString), out type);
}
