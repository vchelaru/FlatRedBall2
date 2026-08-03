using System.Text.Json.Serialization;

namespace RiftboundSample.Models;

public class EquipmentData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Slot { get; set; } = "";  // "weapon", "armor", "accessory"
    public Dictionary<string, int> StatBonuses { get; set; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Element? ElementBonus { get; set; }

    public string? SpecialEffect { get; set; }
}
