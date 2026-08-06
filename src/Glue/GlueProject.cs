using System;
using System.Collections.Generic;
using System.Linq;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// A loaded Glue project: every element addressable by its Glue name, the assets they reference, and
/// the entry points for creating screens and entities from those names.
/// </summary>
/// <remarks>
/// This is the context nothing had before it. A <see cref="GlueScreen"/> holds only its own
/// <c>ScreenSave</c>, so on its own it cannot resolve a nested entity, follow a screen transition, or
/// find an asset — every one of those needs to see the whole project.
/// <para>Element names are Glue's own, backslash-separated (<c>Entities\Player</c>). Lookups accept
/// either separator and ignore case, because these names are typed by hand with no compiler to check
/// them.</para>
/// </remarks>
public sealed class GlueProject
{
    private readonly Dictionary<string, ScreenSave> _screens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EntitySave> _entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GlueEntity>> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GlueLoadDiagnostic> _diagnostics = new();

    private GlueProject(GlueLoadResult result, GlueContentSource? content)
    {
        Result = result;
        Content = content;
        _diagnostics.AddRange(result.Diagnostics);

        foreach (var screen in result.Project.Screens.Where(s => !string.IsNullOrEmpty(s.Name)))
            _screens[screen.Name!] = screen;

        foreach (var entity in result.Project.Entities.Where(e => !string.IsNullOrEmpty(e.Name)))
            _entities[entity.Name!] = entity;
    }

    /// <summary>The raw load result.</summary>
    public GlueLoadResult Result { get; }

    /// <summary>
    /// Everything the load reported, plus anything reported since — applying display settings, for
    /// instance, happens after the load returns.
    /// </summary>
    public IReadOnlyList<GlueLoadDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Where referenced assets come from, when one was supplied.</summary>
    public GlueContentSource? Content { get; }

    /// <summary>The screen the project starts on, or null if it names none that resolved.</summary>
    public ScreenSave? StartUpScreen => Result.StartUpScreen;

    /// <summary>Loads a project from a <c>.gluj</c> path, optionally with a content source.</summary>
    public static GlueProject Load(
        string glujPath, GlueContentSource? content = null, GlueLoadOptions? options = null) =>
        new(GlueProjectLoader.Load(glujPath, options), content);

    /// <summary>
    /// Applies the project's display block to <paramref name="target"/>, usually
    /// <c>FlatRedBallService.Default.DisplaySettings</c>.
    /// </summary>
    /// <remarks>
    /// Call this before starting the first screen — FRB2 applies window properties only on
    /// <c>Start</c>. Glue's display block is project-global, so once is enough.
    /// </remarks>
    public void ApplyDisplaySettings(Rendering.DisplaySettings target)
    {
        var source = Result.Project.DisplaySettings;

        if (source is null)
        {
            _diagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                "This project has no DisplaySettings block, so its resolution and window setup " +
                "could not be applied.",
                null));
            return;
        }

        GlueDisplayMapper.Apply(source, target, _diagnostics);
    }

    /// <summary>The screen with this Glue name, or null.</summary>
    public ScreenSave? FindScreen(string glueName) =>
        _screens.TryGetValue(Normalize(glueName), out var screen) ? screen : null;

    /// <summary>The entity with this Glue name, or null.</summary>
    public EntitySave? FindEntity(string glueName) =>
        _entities.TryGetValue(Normalize(glueName), out var entity) ? entity : null;

    /// <summary>Every live instance created from this entity name.</summary>
    /// <remarks>
    /// Stands in for FRB1's per-entity factory list, and is what a collision relationship binds to.
    /// Pooling and spatial partitioning are <em>not</em> wired — see the Phase 8 notes.
    /// </remarks>
    public IReadOnlyList<GlueEntity> InstancesOf(string glueName) =>
        _instances.TryGetValue(Normalize(glueName), out var list)
            ? list
            : Array.Empty<GlueEntity>();

    /// <summary>
    /// Builds a screen from its Glue name, ready to hand to the engine's screen machinery.
    /// </summary>
    /// <exception cref="ArgumentException">No element has that name.</exception>
    /// <exception cref="InvalidOperationException">The element is abstract.</exception>
    public GlueScreen CreateScreen(string glueName)
    {
        var save = FindScreen(glueName)
            ?? throw UnknownName(glueName, "screen", _screens.Keys);

        if (save.IsAbstract)
        {
            throw new InvalidOperationException(
                $"'{save.Name}' leaves an object for a derived screen to supply, so it is missing " +
                "its own content by construction and cannot be shown on its own.");
        }

        return new GlueScreen { Save = save, Project = this };
    }

    /// <summary>
    /// Creates an entity from its Glue name and registers it on <paramref name="screen"/>.
    /// </summary>
    /// <remarks>
    /// The entity's own objects are built before it is returned, so the caller sees a finished
    /// entity. Registration order matches the engine's: the screen owns it, and destroying it
    /// removes it from this project's instance list.
    /// </remarks>
    /// <exception cref="ArgumentException">No element has that name.</exception>
    /// <exception cref="InvalidOperationException">The element is abstract.</exception>
    public GlueEntity CreateEntity(string glueName, Screen screen)
    {
        var save = FindEntity(glueName)
            ?? throw UnknownName(glueName, "entity", _entities.Keys);

        if (save.IsAbstract)
        {
            throw new InvalidOperationException(
                $"'{save.Name}' leaves an object for a derived entity to supply, so it cannot be " +
                "created on its own.");
        }

        var entity = new GlueEntity { Save = save, Project = this };
        screen.Register(entity);
        entity.BuildObjects();

        Track(save.Name!, entity);
        return entity;
    }

    private void Track(string glueName, GlueEntity entity)
    {
        if (!_instances.TryGetValue(glueName, out var list))
            _instances[glueName] = list = new List<GlueEntity>();

        list.Add(entity);
    }

    /// <summary>Forgets a destroyed instance, so a relationship does not keep collecting corpses.</summary>
    internal void Forget(GlueEntity entity)
    {
        if (entity.GlueName is not null && _instances.TryGetValue(entity.GlueName, out var list))
            list.Remove(entity);
    }

    /// <summary>
    /// Glue writes element names with a backslash. Accepting a forward slash costs one replace and
    /// removes the likeliest thing a hand-typed name gets wrong.
    /// </summary>
    private static string Normalize(string glueName) => glueName.Replace('/', '\\');

    private static ArgumentException UnknownName(
        string requested, string kind, IEnumerable<string> known)
    {
        // Shown plain rather than C#-escaped: the reader is matching this against their project,
        // not pasting it into a string literal.
        string examples = string.Join(", ", known.Take(3).Select(k => $"'{k}'"));

        return new ArgumentException(
            $"No {kind} named '{requested}'. Names are Glue's own and include the folder — " +
            $"for example {examples}.");
    }
}
