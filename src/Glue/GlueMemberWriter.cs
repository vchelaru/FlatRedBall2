using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FlatRedBall2.Glue;

/// <summary>
/// The one place this assembly reflects over a Glue-built object to read or write a member by name.
/// </summary>
/// <remarks>
/// Centralised so the trimmer's view of "what gets reflected over" is a single closed list. Rooting
/// is by <see cref="DynamicDependencyAttribute"/> below, and it must cover every type any caller can
/// reach: the shapes and sprites <see cref="GlueTypeMap"/> constructs, plus the two element types
/// whose own properties a <c>CustomVariable</c> can name.
/// <para><b>Adding a constructible type means adding a matching attribute here</b>, or its
/// properties are trimmed away and every assignment to it silently does nothing in a published
/// build — with no warning at <c>dotnet build</c> time (see the Phase 2 notes on G26).</para>
/// </remarks>
internal static class GlueMemberWriter
{
    /// <summary>
    /// Glue member names that do not match the FRB2 property they set. Relative position is absent
    /// deliberately — it is handled structurally in <see cref="ResolveMemberName"/>.
    /// </summary>
    private static readonly Dictionary<string, string> MemberAliases = new(StringComparer.Ordinal)
    {
        ["Visible"] = "IsVisible",
    };

    /// <summary>What the trimmer must keep: the properties assigned, and the constructor.</summary>
    private const DynamicallyAccessedMemberTypes Rooted =
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor;

    /// <summary>
    /// Maps a Glue member name onto the FRB2 property it addresses.
    /// </summary>
    /// <remarks>
    /// Position is the interesting case, and it resolves more simply than it first appears. FRB1
    /// gives an attached object both an absolute <c>X</c> and a <c>RelativeX</c>, and its codegen
    /// picks between them at assignment time — it emits
    /// <c>if (obj.Parent == null) obj.X = v; else obj.RelativeX = v;</c> whenever the member has a
    /// relative counterpart. FRB2's <c>X</c> already <em>is</em> that branch: an offset from
    /// <c>Parent</c> when one is set, world space when not.
    /// <para>So both Glue members map to the same FRB2 property regardless of attachment, and no
    /// value is dropped. Dropping absolute values would misplace real authored content: DoorsDemo's
    /// player collision box and every Beefball score label are authored exactly that way.</para>
    /// </remarks>
    internal static string ResolveMemberName(string glueMember)
    {
        if (glueMember is "RelativeX" or "RelativeY" or "RelativeZ")
            return glueMember["Relative".Length..];

        return MemberAliases.TryGetValue(glueMember, out string? alias) ? alias : glueMember;
    }

    /// <summary>Finds a public instance property by its already-resolved FRB2 name.</summary>
    [DynamicDependency(Rooted, typeof(Collision.AARect))]
    [DynamicDependency(Rooted, typeof(Collision.Circle))]
    [DynamicDependency(Rooted, typeof(Collision.Polygon))]
    [DynamicDependency(Rooted, typeof(Rendering.Sprite))]
    [DynamicDependency(Rooted, typeof(Entities.CameraControllingEntity))]
    [DynamicDependency(Rooted, typeof(GlueScreen))]
    [DynamicDependency(Rooted, typeof(GlueEntity))]
    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Every reflected type is rooted by the DynamicDependency attributes above, " +
                        "which cover the closed set of types GlueTypeMap can construct plus the two " +
                        "loaded-element types.")]
    internal static PropertyInfo? FindProperty(object target, string memberName) =>
        target.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
}
