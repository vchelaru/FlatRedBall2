namespace RiftboundSample.Models;

public class WorldAbilityData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CharacterId { get; set; } = "";

    /// <summary>Effect key: "reveal_hidden", "unlock_door", "discount_shop", etc.</summary>
    public string Effect { get; set; } = "";
}
