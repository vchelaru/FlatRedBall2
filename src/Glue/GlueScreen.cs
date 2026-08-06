using System;
using System.Collections.Generic;
using System.Text.Json;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// An FRB2 <see cref="Screen"/> built from a Glue <see cref="ScreenSave"/> rather than from a
/// hand-written subclass. Every loaded screen is this one type — what distinguishes them is the data
/// in <see cref="Save"/>, not their class.
/// </summary>
/// <remarks>
/// Start a loaded project by handing the resolved start-up screen to the normal screen machinery:
/// <code>
/// var result = GlueProjectLoader.Load(glujPath);
/// service.Start&lt;GlueScreen&gt;(screen =&gt; screen.Save = result.StartUpScreen);
/// </code>
/// </remarks>
public class GlueScreen : Screen
{
    private readonly Dictionary<string, object> _objects = new();
    private readonly Dictionary<string, JsonElement> _variables = new(StringComparer.OrdinalIgnoreCase);
    private GlueContentSource? _content;
    private readonly List<GlueLoadDiagnostic> _buildDiagnostics = new();

    /// <summary>
    /// The screen data this was built from. Assign it before <c>CustomInitialize</c> runs — the
    /// <c>configure</c> callback on <c>Start</c> and <c>MoveToScreen</c> is the intended place.
    /// </summary>
    public ScreenSave? Save { get; set; }

    /// <summary>
    /// The loaded project this belongs to. Needed to resolve a nested entity, and the usual source
    /// of <see cref="Content"/>.
    /// </summary>
    public GlueProject? Project { get; set; }

    /// <summary>
    /// Where referenced assets come from. Falls back to <see cref="Project"/>'s source, so setting
    /// the project is normally enough. Without either, the element still builds and its file-typed
    /// values are reported and skipped.
    /// </summary>
    public GlueContentSource? Content
    {
        get => _content ?? Project?.Content;
        set => _content = value;
    }

    /// <summary>The Glue element name, in backslash form (<c>Screens\Level1</c>).</summary>
    public string? GlueName => Save?.Name;

    /// <summary>
    /// The objects built from <see cref="Save"/>, keyed by their Glue instance name. Objects whose
    /// type a later phase owns are absent; see <see cref="BuildDiagnostics"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Objects => _objects;

    /// <summary>What could not be built, and why. Warnings here are expected, not failures.</summary>
    public IReadOnlyList<GlueLoadDiagnostic> BuildDiagnostics => _buildDiagnostics;

    /// <summary>
    /// Reads an authored <c>CustomVariable</c> by its Glue name, whether it reached an object, a
    /// property on this screen, or the name/value bag.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="T"/> drives the read — a variable's declared type is often not a CLR type
    /// at all. An unknown name yields <c>default</c> rather than throwing.
    /// </remarks>
    public T? Get<T>(string name) =>
        GlueVariableApplier.Read<T>(name, Save?.CustomVariables, this, _objects, _variables);

    /// <summary>Applies an uncategorized state by name.</summary>
    public void SetState(string stateName) => SetState(null, stateName);

    /// <summary>
    /// Applies a named state, from <paramref name="categoryName"/> when one is given.
    /// </summary>
    /// <remarks>
    /// A state assigns every variable it covers, resetting any it does not name to that variable's
    /// own default — it is a snapshot, not a delta. What it covers comes from the category's
    /// excluded-variable list.
    /// </remarks>
    public void SetState(string? categoryName, string stateName)
    {
        if (Save is null)
            return;

        GlueStateApplier.Apply(
            Save, categoryName, stateName, this, _objects, _variables, _buildDiagnostics);
    }

    /// <inheritdoc />
    public override void CustomInitialize() => BuildObjects();

    /// <summary>
    /// Builds every object in <see cref="Save"/>, registers it on this screen, and applies the
    /// element's variables. Called by <see cref="CustomInitialize"/>; safe to call directly in
    /// tests, where no engine is running.
    /// </summary>
    public void BuildObjects()
    {
        // Unregister anything a previous build added, so a rebuild (hot reload restarts one) does
        // not leave duplicates of every object behind it.
        foreach (var previous in GlueElementBuilder.Flatten(_objects.Values))
        {
            if (previous is Rendering.IRenderable renderable)
                Remove(renderable);
        }

        _objects.Clear();
        _variables.Clear();
        _buildDiagnostics.Clear();

        if (Save is null)
            return;

        // Assets first: an object's instructions name them, so they have to exist by then.
        Content?.Load(Save, _buildDiagnostics);

        GlueElementBuilder.Build(Save.NamedObjects, Save.Name, _objects, _buildDiagnostics,
            addSingle: (builder, save) => builder.AddTo(this, save, Save.Name),
            Content,
            register: RegisterBuilt,
            project: Project,
            owningScreen: OwningScreenForSpawns());

        // Variables run after objects, and after those objects' own instructions, because that is
        // the order FRB1 assigns in — an element variable is expected to win over an instruction.
        GlueVariableApplier.Apply(Save, this, _objects, _variables, _buildDiagnostics);
    }


    /// <summary>The screen a nested entity should be registered on — this one.</summary>
    private Screen OwningScreenForSpawns() => this;

    /// <summary>
    /// Registers an object the tile builder created, which bypasses the normal add path.
    /// </summary>
    /// <remarks>
    /// A <see cref="Tiled.TileMap"/> is not an <c>IRenderable</c> — it has its own overload, because
    /// it owns a layer per Tiled layer. Testing only for <c>IRenderable</c> silently loads the map
    /// and never draws it.
    /// </remarks>
    private void RegisterBuilt(object built)
    {
        switch (built)
        {
            case Tiled.TileMap map: Add(map); break;
            case Rendering.IRenderable renderable: Add(renderable); break;
        }
    }

    /// <inheritdoc />
    public override string ToString() => GlueName ?? nameof(GlueScreen);
}

/// <summary>
/// An FRB2 <see cref="Entity"/> built from a Glue <see cref="EntitySave"/>. As with
/// <see cref="GlueScreen"/>, every loaded entity shares this one type and is distinguished by data.
/// </summary>
public class GlueEntity : Entity, Movement.IPlatformerEntity
{
    private Movement.PlatformerBehavior? _platformer;

    /// <summary>
    /// Platformer movement for this entity.
    /// </summary>
    /// <remarks>
    /// Created on first access rather than eagerly: every loaded entity shares this one type, so a
    /// non-platformer entity would otherwise carry a behavior it never uses. The interface promises
    /// this is never null once the entity is registered, and collision dereferences it during
    /// ground-snap dispatch — so it must materialise rather than return null.
    /// </remarks>
    public Movement.PlatformerBehavior Platformer => _platformer ??= new Movement.PlatformerBehavior();

    private readonly Dictionary<string, object> _objects = new();
    private readonly Dictionary<string, JsonElement> _variables = new(StringComparer.OrdinalIgnoreCase);
    private GlueContentSource? _content;
    private readonly List<GlueLoadDiagnostic> _buildDiagnostics = new();

    /// <summary>The entity data this was built from. Assign it before <c>CustomInitialize</c> runs.</summary>
    public EntitySave? Save { get; set; }

    /// <summary>
    /// The loaded project this belongs to. Needed to resolve a nested entity, and the usual source
    /// of <see cref="Content"/>.
    /// </summary>
    public GlueProject? Project { get; set; }

    /// <summary>
    /// Where referenced assets come from. Falls back to <see cref="Project"/>'s source, so setting
    /// the project is normally enough. Without either, the element still builds and its file-typed
    /// values are reported and skipped.
    /// </summary>
    public GlueContentSource? Content
    {
        get => _content ?? Project?.Content;
        set => _content = value;
    }

    /// <summary>The Glue element name, in backslash form (<c>Entities\Player</c>).</summary>
    public string? GlueName => Save?.Name;

    /// <summary>The objects built from <see cref="Save"/>, keyed by their Glue instance name.</summary>
    public IReadOnlyDictionary<string, object> Objects => _objects;

    /// <summary>What could not be built, and why. Warnings here are expected, not failures.</summary>
    public IReadOnlyList<GlueLoadDiagnostic> BuildDiagnostics => _buildDiagnostics;

    /// <summary>
    /// Reads an authored <c>CustomVariable</c> by its Glue name, whether it reached a contained
    /// object, a property on this entity, or the name/value bag.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="T"/> drives the read — a variable's declared type is often not a CLR type
    /// at all. An unknown name yields <c>default</c> rather than throwing.
    /// </remarks>
    public T? Get<T>(string name) =>
        GlueVariableApplier.Read<T>(name, Save?.CustomVariables, this, _objects, _variables);

    /// <summary>Applies an uncategorized state by name.</summary>
    public void SetState(string stateName) => SetState(null, stateName);

    /// <summary>
    /// Applies a named state, from <paramref name="categoryName"/> when one is given.
    /// </summary>
    /// <remarks>
    /// A state assigns every variable it covers, resetting any it does not name to that variable's
    /// own default — it is a snapshot, not a delta. What it covers comes from the category's
    /// excluded-variable list.
    /// </remarks>
    public void SetState(string? categoryName, string stateName)
    {
        if (Save is null)
            return;

        GlueStateApplier.Apply(
            Save, categoryName, stateName, this, _objects, _variables, _buildDiagnostics);
    }

    /// <inheritdoc />
    public override void CustomInitialize() => BuildObjects();

    /// <summary>
    /// Builds every object in <see cref="Save"/>, attaching those authored to attach, then applies
    /// the element's variables. Called by <see cref="CustomInitialize"/>; safe to call directly in
    /// tests.
    /// </summary>
    public void BuildObjects()
    {
        // See GlueScreen.BuildObjects — a rebuild must not leave the previous children attached.
        foreach (var previous in GlueElementBuilder.Flatten(_objects.Values))
        {
            if (previous is IAttachable attachable)
                Remove(attachable);
        }

        _objects.Clear();
        _variables.Clear();
        _buildDiagnostics.Clear();

        if (Save is null)
            return;

        // Assets first: an object's instructions name them, so they have to exist by then.
        Content?.Load(Save, _buildDiagnostics);

        GlueElementBuilder.Build(Save.NamedObjects, Save.Name, _objects, _buildDiagnostics,
            addSingle: (builder, save) => builder.AddTo(this, save, Save.Name),
            Content,
            register: RegisterBuilt,
            project: Project,
            owningScreen: OwningScreenForSpawns());

        GlueVariableApplier.Apply(Save, this, _objects, _variables, _buildDiagnostics);
    }


    /// <summary>The screen a nested entity should be registered on — the one this entity lives on.</summary>
    private Screen? OwningScreenForSpawns() => _engineOrNull()?.CurrentScreen;

    private Func<FlatRedBallService?> _engineOrNull => () =>
    {
        try { return Engine; } catch (InvalidOperationException) { return null; }
    };

    /// <summary>Registers an object the tile builder created on the screen that owns this entity.</summary>
    /// <remarks>See <see cref="GlueScreen.RegisterBuilt"/> — a tile map needs its own overload.</remarks>
    private void RegisterBuilt(object built)
    {
        switch (built)
        {
            case Tiled.TileMap map: Engine.CurrentScreen.Add(map); break;
            case Rendering.IRenderable renderable: Engine.CurrentScreen.Add(renderable); break;
        }
    }

    /// <inheritdoc />
    public override string ToString() => GlueName ?? nameof(GlueEntity);
}
