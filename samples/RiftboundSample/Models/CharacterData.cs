using System.Text.Json.Serialization;

namespace RiftboundSample.Models;

public enum RowPosition
{
    Front,
    Back
}

public class CharacterData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    // Stats
    public int HP { get; set; }
    public int MP { get; set; }
    public int STR { get; set; }
    public int MAG { get; set; }
    public int DEF { get; set; }
    public int RES { get; set; }
    public int SPD { get; set; }
    public int LCK { get; set; }

    // Progression
    public int Level { get; set; } = 1;
    public int XP { get; set; }
    public int XPToNextLevel { get; set; } = 100;

    // Abilities
    public List<string> AbilityIds { get; set; } = [];

    // Equipment slots (item IDs, empty string = unequipped)
    public string Weapon { get; set; } = "";
    public string Armor { get; set; } = "";
    public string Accessory { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RowPosition Row { get; set; } = RowPosition.Front;

    // Limit Break
    public string? LimitBreakAbilityId { get; set; }

    // World Ability
    public string? WorldAbilityId { get; set; }

    // Stat growth per level
    public GrowthRates? Growth { get; set; }

    // Bond conversations: bond level threshold -> dialogue node ID
    public Dictionary<int, string> BondConversations { get; set; } = [];
}
