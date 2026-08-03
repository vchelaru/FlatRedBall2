using System.Text.Json.Serialization;

namespace RiftboundSample.Models;

public enum StatusEffectType
{
    StatModifier,
    DamageOverTime,
    HealOverTime,
    Stun,
    ElementResist,
    Counter,
    Shield
}

public class StatusEffect
{
    public string Name { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StatusEffectType Type { get; set; } = StatusEffectType.StatModifier;

    /// <summary>Multiplier applied to all stats for StatModifier type. 0.85 = -15%.</summary>
    public float StatMultiplier { get; set; } = 1f;

    /// <summary>Damage/heal per turn for DoT/HoT types, or shield HP for Shield type.</summary>
    public int Amount { get; set; }

    /// <summary>Element for ElementResist type.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Element Element { get; set; } = Element.None;

    /// <summary>Resistance multiplier for ElementResist (e.g. 0.5 = 50% less damage from that element).</summary>
    public float ResistMultiplier { get; set; } = 1f;

    /// <summary>Remaining turns. -1 means permanent until cured.</summary>
    public int RemainingTurns { get; set; }
}
