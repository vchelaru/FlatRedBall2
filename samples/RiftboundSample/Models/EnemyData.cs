using System.Text.Json.Serialization;

namespace RiftboundSample.Models;

public class ElementAffinity
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Element Element { get; set; }

    /// <summary>
    /// Damage multiplier for this element. Values below 1.0 = resistance, above 1.0 = weakness.
    /// </summary>
    public float Multiplier { get; set; } = 1.0f;
}

public class DropEntry
{
    public string ItemId { get; set; } = "";

    /// <summary>Drop probability from 0.0 to 1.0.</summary>
    public float Rate { get; set; }
}

public class EnemyData
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

    public bool IsBoss { get; set; }
    public int XPReward { get; set; }

    public List<string> AbilityIds { get; set; } = [];
    public List<ElementAffinity> ElementAffinities { get; set; } = [];
    public List<DropEntry> DropTable { get; set; } = [];
}
