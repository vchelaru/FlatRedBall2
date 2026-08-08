using System;
using System.Collections.Generic;
using System.Text.Json;
using FlatRedBall2.Glue.Model;
// ToGraphicalUiElement lives in Gum, not Gum.DataTypes with the ScreenSave it extends. The
// MonoGameGum forwarder for it is obsolete.
using Gum;

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
    private readonly Dictionary<string, object?> _runtimeVariables = new(StringComparer.OrdinalIgnoreCase);
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
    /// This screen's Gum UI, once built — null when the screen references no Gum screen, or when Gum
    /// could not produce one.
    /// </summary>
    /// <remarks>
    /// A derived screen inherits its base's Gum screen: DoorsDemo's <c>Level1</c> declares none and
    /// still shows <c>GameScreenGum</c>.
    /// </remarks>
    public Gum.Wireframe.GraphicalUiElement? GumScreen { get; private set; }

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
        GlueVariableApplier.Read<T>(name, Save?.CustomVariables, this, _objects, _variables, _runtimeVariables);

    /// <summary>
    /// Reads or writes an authored <c>CustomVariable</c> by its Glue name — the property bag a
    /// loaded element carries instead of the generated properties FRB1 would have compiled.
    /// </summary>
    /// <remarks>
    /// A name matching a real member of this element reaches that member, so
    /// <c>element["X"] = 5f</c> moves it; a tunneling variable reaches the contained object it
    /// targets; anything else is held by name. The getter returns the value as stored — use
    /// <see cref="Get{T}"/> to read it as a particular type.
    /// </remarks>
    public object? this[string name]
    {
        get => Get<object>(name);
        set => GlueVariableApplier.Write(
            name, value, Save?.CustomVariables, this, _objects, _runtimeVariables);
    }

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
    public override void CustomInitialize()
    {
        BuildObjects();
        WatchGlueProject();
    }

    // Glue writes into the project's source tree while the game reads the build output, so a save
    // in Glue is invisible until the file is copied across and the project reparsed. One watcher
    // rooted at the .gluj's own directory covers all of it: the .gluj, the .glsj/.glej element
    // files Glue writes beside it, and the assets under Content/.
    private void WatchGlueProject()
    {
        if (Engine is not { IsGlueHotReloadEnabled: true } engine) return;
        if (engine.GlueProjectFile is not string glujPath) return;
        // The watch maps the project directory onto each source root, which only means anything for
        // a path relative to the output folder — and GlueContentSource requires that anyway.
        if (System.IO.Path.IsPathRooted(glujPath)) return;

        var watcher = WatchContentDirectoryContaining(
            anchorFile: glujPath,
            sourceDirectory: System.IO.Path.GetDirectoryName(glujPath) ?? string.Empty,
            _ => ReloadAndRestart());
        if (watcher is null) return;

        foreach (var extension in GlueAuthoredExtensions)
            watcher.AutoCopyExtensions.Add(extension);
        // Gum patches its own files in place. Restarting the screen underneath that pass would tear
        // down the visuals it just updated.
        foreach (var gumExtension in GumOwnedExtensions)
            watcher.IgnoredExtensions.Add(gumExtension);
    }

    /// <summary>
    /// File types Glue creates that must reach the build output even when no copy is there yet.
    /// </summary>
    /// <remarks>
    /// The dest-exists gate assumes a new source file is editor scratch. In a Glue project the
    /// opposite holds — a screen, entity, or asset appearing in the tree is the editor doing its
    /// job — and without this, "add something in Glue" would be the one edit hot reload cannot see.
    /// </remarks>
    private static readonly string[] GlueAuthoredExtensions =
    {
        ".gluj", ".glsj", ".glej",       // project and element files
        ".tmx", ".csv", ".achx",         // assets GlueContentSource loads (.png/.tsx already default)
    };

    // Gum's XML file types and their JSON successors. Gum's own hot-reload pipeline owns both.
    private static readonly string[] GumOwnedExtensions =
    {
        ".gumx", ".gusx", ".gucx", ".gutx", ".behx", ".ganx",
        ".gumj", ".gusj", ".gucj", ".gutj", ".behj", ".ganj",
    };

    private bool _reloadRequested;

    /// <summary>
    /// Queues a restart onto the same Glue screen, rebuilt from the project as it stands on disk.
    /// </summary>
    /// <remarks>
    /// Guarded because one save in Glue touches several files and the watcher reports each of them.
    /// The guard needs no reset: the restart replaces this screen instance.
    /// </remarks>
    private void ReloadAndRestart()
    {
        if (_reloadRequested) return;
        _reloadRequested = true;

        // The configure the game passed to Start closes over the project loaded at boot, so a plain
        // RestartScreen would rebuild from the pre-edit data. This one reparses instead, and stays
        // in place for later restarts.
        var engine = Engine;
        var glueName = GlueName;
        engine.RequestScreenRestart(
            screen => ApplyReloadedProject((GlueScreen)screen, engine, glueName), RestartMode.HotReload);
    }

    /// <summary>
    /// Reparses the project and points a restarted screen at it, keeping it on the same Glue screen.
    /// </summary>
    /// <remarks>
    /// The reparse happens here rather than when the file event fired because one save in Glue writes
    /// several files and the watcher copies them one at a time — reading mid-batch would see a tree
    /// that is only half updated. By the time this queued restart runs, every copy has landed.
    /// <para>
    /// Falls back to the start-up screen when the name is gone: renaming a screen in Glue is a normal
    /// edit, and booting somewhere beats booting nowhere.
    /// </para>
    /// </remarks>
    internal static void ApplyReloadedProject(GlueScreen screen, FlatRedBallService engine, string? glueName)
    {
        engine.ReloadGlueProject();
        var project = engine.GlueProject;
        screen.Project = project;
        screen.Save = (glueName is null ? null : project?.FindScreen(glueName)) ?? project?.StartUpScreen;
    }

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

        BuildGumScreen();
        BuildTileEntities();
    }

    /// <summary>
    /// Spawns entities for tiles whose type names one, for every map this screen built.
    /// </summary>
    /// <remarks>
    /// Last, because a spawned entity is an ordinary instance that the screen must already own —
    /// and because the maps have to exist before their tiles can be scanned.
    /// </remarks>
    private void BuildTileEntities()
    {
        if (Project is null)
            return;

        foreach (var built in _objects.Values)
        {
            if (built is Tiled.TileMap map)
                GlueTileBuilder.CreateEntitiesFromTiles(map, Project, this, _buildDiagnostics, Save?.Name);
        }
    }

    /// <summary>
    /// Adds the Gum screen this element references, if any. Requires a Gum project that Gum itself
    /// loaded — see <see cref="EngineInitSettings.GlueProjectFile"/>.
    /// </summary>
    private void BuildGumScreen()
    {
        GumScreen = null;

        if (Save is null || Project is null)
            return;

        string? elementName = GlueGumResolver.GumElementNameFor(
            Save, Project.Result.Project, Project.Result.GumProjectFile);

        if (elementName is null)
            return;

        var gumFile = Save.ReferencedFiles.Find(GlueGumResolver.IsGumElement);
        if (gumFile is not null && GlueGumResolver.IsLegacyGumIdb(gumFile))
        {
            _buildDiagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                $"'{gumFile.Name}' loads through the legacy GumIdb model, which reads the file " +
                "directly rather than looking the element up in the Gum project. Loading it as an " +
                "ordinary Gum screen instead.",
                Save.Name));
        }

        var screenSave = Gum.Managers.ObjectFinder.Self.GumProjectSave?.Screens
            .Find(s => string.Equals(s.Name, elementName, StringComparison.OrdinalIgnoreCase));

        if (screenSave is null)
        {
            _buildDiagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                $"Gum screen '{elementName}' was referenced but is not in the loaded Gum project. " +
                "Gum only resolves elements from a project it loaded itself — set " +
                $"{nameof(EngineInitSettings)}.{nameof(EngineInitSettings.GlueProjectFile)} so it does.",
                Save.Name));
            return;
        }

        GumScreen = screenSave.ToGraphicalUiElement();
        Add(GumScreen);
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

    private Movement.TopDownBehavior? _topDown;
    private Dictionary<string, Movement.TopDownValues>? _topDownMovements;

    /// <summary>
    /// Top-down movement for this entity. Created on first access, for the same reason
    /// <see cref="Platformer"/> is — every loaded entity shares one type.
    /// </summary>
    /// <remarks>
    /// Snaps four-way rather than the engine default of eight. FRB1's generated <c>Initialize</c>
    /// hard-sets <c>PossibleDirections = FourWay</c> unconditionally, ignoring the CSV, and Glue
    /// persists no per-entity direction setting at all — so a loaded entity keeping FRB2's
    /// eight-way default would move diagonally where the same project does not in FRB1 (G114).
    /// </remarks>
    public Movement.TopDownBehavior TopDown =>
        _topDown ??= new Movement.TopDownBehavior { DirectionSnap = Movement.DirectionSnap.FourWay };

    /// <summary>
    /// Gives this entity a named set of top-down movement values and selects the first.
    /// </summary>
    /// <remarks>
    /// First rather than none, because Glue's generated code defaults to the first dictionary entry —
    /// an entity with a movement CSV and no selection still moves.
    /// </remarks>
    internal void SetTopDownMovementSet(IReadOnlyDictionary<string, Movement.TopDownValues> values)
    {
        _topDownMovements = new Dictionary<string, Movement.TopDownValues>(
            values, StringComparer.OrdinalIgnoreCase);

        foreach (var value in values.Values)
        {
            TopDown.MovementValues = value;
            break;
        }
    }

    /// <summary>
    /// Selects one of this entity's authored top-down movement sets by name.
    /// </summary>
    /// <remarks>
    /// An unknown name leaves the current values in place rather than clearing them — a data-driven
    /// name has no compiler checking it, and a typo that stopped the entity dead would read as a
    /// movement bug rather than a bad string.
    /// </remarks>
    public void SetTopDownMovement(string movementName)
    {
        if (_topDownMovements is not null &&
            _topDownMovements.TryGetValue(movementName, out var values))
        {
            TopDown.MovementValues = values;
        }
    }

    private readonly Dictionary<string, object> _objects = new();
    private readonly Dictionary<string, JsonElement> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _runtimeVariables = new(StringComparer.OrdinalIgnoreCase);
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
        GlueVariableApplier.Read<T>(name, Save?.CustomVariables, this, _objects, _variables, _runtimeVariables);

    /// <summary>
    /// Reads or writes an authored <c>CustomVariable</c> by its Glue name — the property bag a
    /// loaded element carries instead of the generated properties FRB1 would have compiled.
    /// </summary>
    /// <remarks>
    /// A name matching a real member of this element reaches that member, so
    /// <c>element["X"] = 5f</c> moves it; a tunneling variable reaches the contained object it
    /// targets; anything else is held by name. The getter returns the value as stored — use
    /// <see cref="Get{T}"/> to read it as a particular type.
    /// </remarks>
    public object? this[string name]
    {
        get => Get<object>(name);
        set => GlueVariableApplier.Write(
            name, value, Save?.CustomVariables, this, _objects, _runtimeVariables);
    }

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

        LoadTopDownValues();
        LoadClimbingMovement();
    }

    /// <summary>
    /// Loads a top-down entity's movement CSV, which nothing else names.
    /// </summary>
    /// <remarks>
    /// The platformer slots arrive as <c>CustomVariable</c>s naming a row (<c>"Ground in X.csv"</c>),
    /// so they resolve through the variable applier. Top-down has no such variable: Glue records only
    /// <c>IsTopDown</c> and loads the whole CSV, defaulting to its first row. Discovery therefore has
    /// to start from the property rather than from a variable.
    /// </remarks>
    private void LoadTopDownValues()
    {
        if (Save is null || !Save.Properties.GetValue<bool>("IsTopDown"))
            return;

        var csvFile = Save.ReferencedFiles.Find(
            f => f.Name is not null &&
                 f.Name.EndsWith("TopDownValuesStatic.csv", StringComparison.OrdinalIgnoreCase));

        if (csvFile is null)
        {
            _buildDiagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                "This entity is marked top-down but references no TopDownValuesStatic.csv, so it " +
                "has no movement values.", Save.Name));
            return;
        }

        // Name is non-null by the Find predicate above, which matched on its suffix.
        string? csv = Content?.GetText(
            System.IO.Path.GetFileNameWithoutExtension(csvFile.Name!));

        if (csv is null)
        {
            _buildDiagnostics.Add(new GlueLoadDiagnostic(
                GlueDiagnosticSeverity.Warning,
                $"'{csvFile.Name}' holds this entity's top-down movement values but was not loaded.",
                Save.Name));
            return;
        }

        GlueMovementValues.ApplyTopDown(this, GlueMovementValues.ReadTopDown(csv));
    }

    /// <summary>
    /// Fills the platformer's climbing slot from the CSV row that opts into climbing.
    /// </summary>
    /// <remarks>
    /// FRB1 has no climbing slot — <c>CanClimb</c> is a per-row bool, and its generated code expects
    /// game code to swap that row into the ground slot when the character is on a ladder. FRB2 owns
    /// the ladder state itself (<see cref="Movement.PlatformerBehavior.Ladders"/>) and reads
    /// <see cref="Movement.PlatformerBehavior.ClimbingMovement"/> whenever it is climbing, so the row
    /// can simply be assigned. Nothing happens until the behaviour is given ladders, which is the
    /// game's call.
    /// </remarks>
    private void LoadClimbingMovement()
    {
        if (Save is null || Platformer.ClimbingMovement is not null)
            return;

        var csvFile = Save.ReferencedFiles.Find(
            f => f.Name is not null &&
                 f.Name.EndsWith("PlatformerValuesStatic.csv", StringComparison.OrdinalIgnoreCase));

        string? csv = csvFile?.Name is null
            ? null
            : Content?.GetText(System.IO.Path.GetFileNameWithoutExtension(csvFile.Name));

        if (csv is null)
            return;

        Platformer.ClimbingMovement = GlueMovementValues.FindClimbingRow(csv);
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
