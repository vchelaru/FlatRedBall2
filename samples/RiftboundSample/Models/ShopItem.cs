namespace RiftboundSample.Models;

public class ShopItem
{
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }
    public string Category { get; set; } = "";  // "weapon", "armor", "accessory", "consumable"
}
