using System.Text.Json;
using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class ShopSystem
{
    private readonly Dictionary<string, ShopItem> _shopItems = [];
    private readonly Dictionary<string, EquipmentData> _equipmentLookup;

    public ShopSystem(Dictionary<string, EquipmentData> equipmentLookup)
    {
        _equipmentLookup = equipmentLookup;
    }

    public List<ShopItem> CurrentStock { get; private set; } = [];

    public void SetStock(List<ShopItem> items)
    {
        CurrentStock = items;
        foreach (var item in items)
            _shopItems[item.ItemId] = item;
    }

    public void LoadShop(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string json = File.ReadAllText(DataPath.Resolve(path));
        var items = JsonSerializer.Deserialize<List<ShopItem>>(json, options) ?? [];
        CurrentStock = items;
        foreach (var item in items)
            _shopItems[item.ItemId] = item;
    }

    public bool CanBuy(PartyState party, ShopItem item) => party.Gold >= item.BuyPrice;

    public bool Buy(PartyState party, ShopItem item)
    {
        if (party.Gold < item.BuyPrice) return false;
        party.Gold -= item.BuyPrice;
        party.AddItem(item.ItemId);
        return true;
    }

    public bool Sell(PartyState party, string itemId)
    {
        if (!_shopItems.TryGetValue(itemId, out var item)) return false;
        if (!party.RemoveItem(itemId)) return false;
        party.Gold += item.SellPrice;
        return true;
    }

    /// <summary>
    /// Returns stat difference when equipping an item vs current equipment.
    /// Positive values mean improvement.
    /// </summary>
    public Dictionary<string, int> CompareEquipment(PartyState party, string characterId, string itemId)
    {
        var diff = new Dictionary<string, int>();
        if (!_equipmentLookup.TryGetValue(itemId, out var newEquip)) return diff;

        // Copy new item bonuses
        foreach (var (stat, value) in newEquip.StatBonuses)
            diff[stat] = value;

        // Subtract current equipment bonuses
        var currentId = party.GetEquipped(characterId, newEquip.Slot);
        if (currentId != null && _equipmentLookup.TryGetValue(currentId, out var oldEquip))
        {
            foreach (var (stat, value) in oldEquip.StatBonuses)
            {
                if (diff.ContainsKey(stat))
                    diff[stat] -= value;
                else
                    diff[stat] = -value;
            }
        }

        return diff;
    }

    public EquipmentData? GetEquipment(string itemId)
        => _equipmentLookup.GetValueOrDefault(itemId);
}
