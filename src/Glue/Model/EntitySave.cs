using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>The contents of one Glue <c>.glej</c> file.</summary>
public class EntitySave : GlueElement
{
    private string? _baseEntity;

    /// <summary>
    /// The entity this one derives from, in the same backslash form as <see cref="GlueElement.Name"/>.
    /// </summary>
    /// <remarks>
    /// May name an engine type rather than another element (<c>FlatRedBall.Sprite</c>), which has no
    /// data-driven equivalent and is reported rather than resolved.
    /// </remarks>
    public string? BaseEntity
    {
        get => _baseEntity;
        set => _baseEntity = GlueSentinel.NullIfUnset(value);
    }

    /// <inheritdoc />
    [JsonIgnore]
    public override string? BaseElement => BaseEntity;

    /// <summary>Whether a factory is generated so other entities can spawn this one. Phase 8.</summary>
    public bool CreatedByOtherEntities { get; set; }

    /// <summary>Whether the factory pools instances rather than allocating per spawn. Phase 8.</summary>
    public bool PooledByFactory { get; set; }

    /// <summary>Whether this entity participates in collision. Phase 9.</summary>
    /// <remarks>
    /// Bag-backed, unlike its three siblings below, which FRB1 declares as ordinary members. Bound
    /// as a JSON member this reads <c>false</c> for every project that sets it.
    /// </remarks>
    [JsonIgnore]
    public bool ImplementsICollidable => Properties.GetValue<bool>(nameof(ImplementsICollidable));

    /// <summary>Whether this entity handles clicks.</summary>
    public bool ImplementsIClickable { get; set; }

    /// <summary>Whether this entity exposes visibility.</summary>
    public bool ImplementsIVisible { get; set; }

    /// <summary>Whether this entity carries Tiled tile metadata. Phase 10.</summary>
    public bool ImplementsITiledTileMetadata { get; set; }

    /// <summary>Whether the entity is treated as 2D.</summary>
    public bool Is2D { get; set; }

    /// <summary>
    /// Which input device drives this entity, when it has movement behavior. Stored in
    /// <see cref="GlueElement.Properties"/> rather than as its own JSON member. Mapped in Phase 11.
    /// </summary>
    [JsonIgnore]
    public int InputDevice => Properties.GetValue<int>(nameof(InputDevice));
}
