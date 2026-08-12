using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Builds real FRB2 objects from Glue <see cref="NamedObjectSave"/> data: constructs the instance,
/// applies its authored values, and optionally attaches and registers it.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="GlueScreen"/> and <see cref="GlueEntity"/> — both need it,
/// and keeping it standalone means it can be tested without a running engine.
/// <para>Anything it cannot build reports a diagnostic and is skipped. A project full of types later
/// phases own still loads and still shows what it can.</para>
/// </remarks>
public sealed class GlueObjectBuilder
{
    private readonly ICollection<GlueLoadDiagnostic> _diagnostics;
    private readonly GlueContentSource? _content;
    private readonly GlueProject? _project;
    private readonly Screen? _owningScreen;

    /// <summary>Creates a builder that reports what it cannot handle into <paramref name="diagnostics"/>.</summary>
    /// <remarks>
    /// <c>content</c> resolves instruction values that name a referenced asset. Without one, those
    /// values are reported and skipped rather than failing the build.
    /// </remarks>
    public GlueObjectBuilder(
        ICollection<GlueLoadDiagnostic> diagnostics,
        GlueContentSource? content = null,
        GlueProject? project = null,
        Screen? owningScreen = null)
    {
        _diagnostics = diagnostics;
        _content = content;
        _project = project;
        _owningScreen = owningScreen;
    }

    /// <summary>
    /// Constructs and configures an instance without attaching or registering it.
    /// </summary>
    /// <returns>The configured instance, or null if this build cannot construct that type.</returns>
    public object? Create(NamedObjectSave save, string? elementName = null)
    {
        var typeName = GlueTypeName.Parse(save.SourceClassType);

        // A Gum object is identified before the type map is consulted: its SourceClassType names a
        // generated runtime class that exists only in FRB1, so the map could never resolve it.
        if (GlueGumResolver.ComponentElementNameFor(save, _project?.Result.GumProjectFile) is string gumElementName)
            return CreateGumVisual(save, gumElementName, elementName);

        // An object whose type names another element is a nested entity — built from that element's
        // own data rather than constructed from a CLR type.
        if (typeName.IsElementReference)
            return CreateNestedEntity(save, elementName);

        if (!GlueTypeMap.TryCreate(typeName, out object? instance))
        {
            Warn($"'{save.InstanceName}' is a '{save.SourceClassType}', which cannot be built by " +
                 "this build. A later phase owns this type.", elementName);
            return null;
        }

        ApplyShapeVisibilityDefault(instance);
        ApplyInstructions(instance, save, elementName);

        // A Polygon starts with no points and its draw call bails below two, so it would be present,
        // positioned, and invisible with nothing to say why.
        if (instance is Collision.Polygon { Points.Count: < 2 })
        {
            Warn($"'{save.InstanceName}' is a Polygon with fewer than two points, so it will not " +
                 "render. Glue authors the geometry as a 'Points' instruction whose value is an " +
                 "array of \"x, y\" strings; decoding that shape is not supported yet.", elementName);
        }

        return instance;
    }

    /// <summary>
    /// Constructs an instance and adds it to <paramref name="container"/>, attaching it when the save
    /// says to. Attachment both parents the object and registers it for rendering.
    /// </summary>
    public object? AddTo(Entity container, NamedObjectSave save, string? elementName = null)
    {
        object? instance = Create(save, elementName);

        // A Gum visual is not IAttachable — it is parented through the entity's own Gum support,
        // which is what keeps it following the entity.
        if (instance is Gum.Wireframe.GraphicalUiElement visual)
        {
            container.Add(visual);
            return instance;
        }

        if (instance is not IAttachable attachable || !save.AttachToContainer)
            return instance;

        // Glue lets a shape be attached for position and rendering without taking part in the
        // entity's collision; FRB2's plain Add opts every shape in, so honour the flag. The
        // opt-out overload is generic over "attachable and collidable", which no single interface
        // expresses — hence the switch over the closed set of shape types.
        if (!save.IncludeInICollidable)
        {
            switch (instance)
            {
                case Collision.AARect rect: container.Add(rect, isDefaultCollision: false); return instance;
                case Collision.Circle circle: container.Add(circle, isDefaultCollision: false); return instance;
                case Collision.Polygon polygon: container.Add(polygon, isDefaultCollision: false); return instance;
            }
        }

        container.Add(attachable);
        return instance;
    }

    /// <summary>Constructs an instance and registers it directly on a screen.</summary>
    /// <remarks>
    /// An entity is <see cref="Screen.Register(Entity)"/>ed rather than added: registration is what
    /// gives it per-frame activity. A camera controller that is added but not registered loads
    /// correctly, points at the right target, and never moves.
    /// </remarks>
    public object? AddTo(Screen container, NamedObjectSave save, string? elementName = null)
    {
        object? instance = Create(save, elementName);

        switch (instance)
        {
            // A GlueEntity arrives already owned: it is only ever created through
            // GlueProject.CreateEntity, which registers it on this screen and builds its contents.
            // Registering it again lands it in the entity list twice and re-adds its children to the
            // render list, so one authored instance updates and draws as two.
            case GlueEntity:
                break;

            case Entity entity:
                container.Register(entity);

                // Register wires the entity up but does not initialise it — the engine's own
                // Factory does that as a separate step. Skipping it leaves an engine entity in a
                // half-built state: a camera controller resolves its Camera in CustomInitialize and
                // throws on the screen's first frame without it.
                // Guarded because a test can build a screen with no engine behind it, and an
                // entity's initialiser is entitled to expect one.
                if (container.Engine is not null)
                    entity.CustomInitialize();

                break;

            case Gum.Wireframe.GraphicalUiElement visual:
                container.Add(visual);
                break;

            case Rendering.IRenderable renderable:
                container.Add(renderable);
                break;
        }

        return instance;
    }

    /// <summary>
    /// FRB2 shapes default to invisible because they are primarily collision volumes; a shape
    /// authored in Glue is meant to be seen. Applied before instructions so an explicit
    /// <c>Visible</c> instruction still wins.
    /// </summary>
    private static void ApplyShapeVisibilityDefault(object instance)
    {
        switch (instance)
        {
            case Collision.AARect rect: rect.IsVisible = true; break;
            case Collision.Circle circle: circle.IsVisible = true; break;
            case Collision.Polygon polygon: polygon.IsVisible = true; break;
        }
    }

    internal void ApplyInstructions(object instance, NamedObjectSave save, string? elementName)
    {
        foreach (var instruction in save.InstructionSaves)
        {
            if (string.IsNullOrEmpty(instruction.Member))
                continue;

            string memberName = GlueMemberWriter.ResolveMemberName(instruction.Member);

            // The camera controller's own members are applied later, once the objects they name
            // exist. Letting the ordinary pass see them produces four warnings about nothing.
            if (instance is Entities.CameraControllingEntity)
                continue;

            // Some Glue members are methods in FRB2 rather than properties, and some values name a
            // loaded asset rather than carrying one. Both are handled before ordinary reflection.
            if (TryApplyAsAction(instance, memberName, instruction, save, elementName))
                continue;

            if (TryApplyAsAsset(instance, memberName, instruction, save, elementName))
                continue;

            var property = GlueMemberWriter.FindProperty(instance, memberName);

            if (property is null || !property.CanWrite)
            {
                Warn($"'{save.InstanceName}' has no writable '{memberName}' " +
                     $"(from Glue member '{instruction.Member}'); the value was skipped.", elementName);
                continue;
            }

            if (!GlueValueConverter.TryConvert(instruction.Value, property.PropertyType, out object? converted))
            {
                Warn($"'{save.InstanceName}.{memberName}' could not take the authored value " +
                     $"'{instruction.Value}' as {property.PropertyType.Name}; the default was kept.",
                    elementName);
                continue;
            }

            property.SetValue(instance, converted);
        }
    }



    /// <summary>
    /// Builds a nested entity instance from the element its <c>SourceClassType</c> names.
    /// </summary>
    /// <remarks>
    /// Needs both a project (to find the element) and a screen (to own the instance). Without
    /// either, this reports and skips — which is what every build did before a project context
    /// existed.
    /// </remarks>
    private object? CreateNestedEntity(NamedObjectSave save, string? elementName)
    {
        if (_project is null || _owningScreen is null)
        {
            Warn($"'{save.InstanceName}' is an instance of '{save.SourceClassType}', which needs a " +
                 "loaded project to resolve; it was skipped.", elementName);
            return null;
        }

        var referenced = _project.FindEntity(save.SourceClassType ?? string.Empty);

        if (referenced is null)
        {
            Warn($"'{save.InstanceName}' is an instance of '{save.SourceClassType}', which is not " +
                 "in this project.", elementName);
            return null;
        }

        if (referenced.IsAbstract)
        {
            Warn($"'{save.InstanceName}' is an instance of '{save.SourceClassType}', which is " +
                 "abstract and cannot be created on its own.", elementName);
            return null;
        }

        var entity = _project.CreateEntity(referenced.Name!, _owningScreen);

        // The instance's own instructions layer on top of the entity's authored values.
        ApplyInstructions(entity, save, elementName);
        return entity;
    }

    /// <summary>
    /// Builds a Gum component instance from the loaded Gum project.
    /// </summary>
    /// <remarks>
    /// The element has to come from a project Gum itself loaded — see
    /// <see cref="EngineInitSettings.GlueProjectFile"/>. Without one this reports and skips, the same
    /// as any other type this build cannot construct.
    /// </remarks>
    private object? CreateGumVisual(NamedObjectSave save, string gumElementName, string? elementName)
    {
        var gumElement = GlueGumResolver.FindGumElement(gumElementName);

        if (gumElement is null)
        {
            Warn($"'{save.InstanceName}' is the Gum element '{gumElementName}', which is not in the " +
                 "loaded Gum project. Gum only resolves elements from a project it loaded itself — " +
                 $"set {nameof(EngineInitSettings)}.{nameof(EngineInitSettings.GlueProjectFile)} so " +
                 "it does.", elementName);
            return null;
        }

        var visual = Gum.ElementSaveExtensionMethods.ToGraphicalUiElement(gumElement, null);
        ApplyInstructions(visual, save, elementName);
        return visual;
    }

    /// <summary>
    /// Applies a Glue member whose FRB2 equivalent is a method call rather than a property.
    /// </summary>
    /// <remarks>
    /// <c>CurrentChainName</c> is the case that matters: FRB1 assigns a property, FRB2 exposes
    /// <c>PlayAnimation(string)</c>, and property reflection cannot reach a method. Glue orders
    /// instructions so the chain list is assigned first, which this relies on.
    /// </remarks>
    private bool TryApplyAsAction(
        object instance, string memberName, InstructionSave instruction,
        NamedObjectSave save, string? elementName)
    {
        if (memberName != "CurrentChainName" || instance is not Rendering.Sprite sprite)
            return false;

        string? chainName = instruction.Value.ValueKind == JsonValueKind.String
            ? instruction.Value.GetString()
            : null;

        if (string.IsNullOrEmpty(chainName))
            return true;

        if (sprite.AnimationChains is null)
        {
            Warn($"'{save.InstanceName}' names the animation '{chainName}' but has no animation " +
                 "list; the chain was not played.", elementName);
            return true;
        }

        sprite.PlayAnimation(chainName);
        return true;
    }

    /// <summary>
    /// Resolves an instruction whose value names a referenced asset by instance name rather than
    /// carrying a value of its own.
    /// </summary>
    private bool TryApplyAsAsset(
        object instance, string memberName, InstructionSave instruction,
        NamedObjectSave save, string? elementName)
    {
        var property = GlueMemberWriter.FindProperty(instance, memberName);

        if (property is null || !property.CanWrite || !IsAssetType(property.PropertyType))
            return false;

        string? assetName = instruction.Value.ValueKind == JsonValueKind.String
            ? instruction.Value.GetString()
            : null;

        if (string.IsNullOrEmpty(assetName))
            return true;

        if (_content is null)
        {
            Warn($"'{save.InstanceName}.{memberName}' names the asset '{assetName}', but no content " +
                 "source was supplied; the default was kept.", elementName);
            return true;
        }

        object? asset = property.PropertyType == typeof(Texture2D)
            ? _content.Get<Texture2D>(assetName)
            : _content.Get<Animation.AnimationChainList>(assetName);

        if (asset is null)
        {
            Warn($"'{save.InstanceName}.{memberName}' names the asset '{assetName}', which was not " +
                 "loaded; the default was kept.", elementName);
            return true;
        }

        property.SetValue(instance, asset);
        return true;
    }

    /// <summary>Whether a property holds a loaded asset rather than a plain value.</summary>
    private static bool IsAssetType(Type type) =>
        type == typeof(Texture2D) || type == typeof(Animation.AnimationChainList);

    private void Warn(string message, string? elementName) =>
        _diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
