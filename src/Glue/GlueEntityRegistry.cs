using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FlatRedBall2.Glue;

/// <summary>
/// Maps Glue entity names to the C# classes that carry their code-behind, the counterpart of
/// FRB1's hand-written half of a generated entity partial class.
/// </summary>
/// <remarks>
/// An entity with no registration is a plain <see cref="GlueEntity"/>. A registered class derives
/// from <see cref="GlueEntity"/>, so everything a loaded entity does (variables, states, lists,
/// collision, movement) works unchanged, and the class adds members and behaviour on top.
/// <para>The registry belongs to an engine, not to a project: <see cref="FlatRedBallService.GlueEntities"/>
/// hands the same instance to every project the engine loads, so registrations survive a hot reload
/// and can be made before <c>Initialize</c> loads the project. A project loaded without an engine
/// has its own.</para>
/// <para>Lifecycle mirrors <see cref="Factory{T}"/> so a registered class and a hand-written entity
/// mean the same thing by the same name: <see cref="Entity.CustomInitialize"/> runs once per
/// instance, after the Glue data is applied, and <c>Reset()</c> runs on each reuse of a pooled
/// instance. FRB1's <c>CustomInitialize</c> ran on every reuse, so its per-life code belongs in
/// <c>Reset()</c> here.</para>
/// </remarks>
public sealed class GlueEntityRegistry
{
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.OrdinalIgnoreCase);

    private readonly record struct Registration(Func<GlueEntity> Create, Type Type);

    /// <summary>
    /// Makes every entity named <paramref name="glueName"/> an instance of <typeparamref name="T"/>.
    /// Registering a name again replaces the earlier class.
    /// </summary>
    /// <remarks>
    /// An entity derived from the named one in Glue uses this class too, unless it has a
    /// registration of its own, because in FRB1 a derived entity inherits its base's code-behind.
    /// Register the derived class as a subclass of the base one.
    /// <para>Properties on <typeparamref name="T"/> are what a Glue variable or per-instance override
    /// of the same name is written to. A variable the class has no property for is kept by name
    /// instead (<see cref="GlueEntity.Get{T}"/>); an override it cannot take is reported in
    /// <see cref="GlueEntity.BuildDiagnostics"/> or the owning screen's.</para>
    /// </remarks>
    /// <param name="glueName">The entity's Glue name, with its folder: <c>Entities\Player</c>.</param>
    /// <returns>This registry, so registrations can be chained.</returns>
    public GlueEntityRegistry Register<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
            DynamicallyAccessedMemberTypes.PublicProperties)] T>(string glueName)
        where T : GlueEntity, new()
    {
        ArgumentException.ThrowIfNullOrEmpty(glueName);
        _registrations[GlueProject.Normalize(glueName)] = new Registration(static () => new T(), typeof(T));
        return this;
    }

    /// <summary>The Glue names that have a class registered, in Glue's backslash form.</summary>
    public IEnumerable<string> RegisteredNames => _registrations.Keys;

    /// <summary>Whether a class is registered for exactly this Glue name.</summary>
    public bool IsRegistered(string glueName) => _registrations.ContainsKey(GlueProject.Normalize(glueName));

    /// <summary>A new instance of the class registered for this exact name, or null.</summary>
    internal GlueEntity? TryCreate(string glueName) =>
        _registrations.TryGetValue(GlueProject.Normalize(glueName), out var registration)
            ? registration.Create()
            : null;
}
