using System.Text.Json.Serialization;

namespace RiftboundSample.Models;

public enum TargetType
{
    SingleEnemy,
    AllEnemies,
    SingleAlly,
    AllAllies,
    Self
}

public enum DamageType
{
    Physical,
    Magical,
    Healing,
    Buff,
    Debuff
}

public enum Element
{
    None,
    Steam,
    Glitch,
    Aether,
    Fire,
    Ice,
    Lightning
}

public class AbilityData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int MPCost { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TargetType TargetType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DamageType DamageType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Element Element { get; set; }

    public float Multiplier { get; set; } = 1.0f;
}
