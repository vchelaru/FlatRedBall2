namespace RiftboundSample.Models;

public class PartyState
{
    /// <summary>All recruited character IDs.</summary>
    public List<string> Roster { get; set; } = [];

    /// <summary>Active party slots (max 4 character IDs).</summary>
    public List<string> ActiveParty { get; set; } = [];

    /// <summary>Item ID to quantity.</summary>
    public Dictionary<string, int> Inventory { get; set; } = [];

    /// <summary>Runtime state for all party pets.</summary>
    public List<PetState> Pets { get; set; } = [];

    public int Gold { get; set; }

    /// <summary>Character ID -> slot -> equipped item ID.</summary>
    public Dictionary<string, Dictionary<string, string>> EquippedItems { get; set; } = [];

    public string? GetEquipped(string characterId, string slot)
    {
        if (EquippedItems.TryGetValue(characterId, out var slots)
            && slots.TryGetValue(slot, out var itemId)
            && !string.IsNullOrEmpty(itemId))
            return itemId;
        return null;
    }

    public void Equip(string characterId, string slot, string itemId)
    {
        if (!EquippedItems.ContainsKey(characterId))
            EquippedItems[characterId] = [];
        EquippedItems[characterId][slot] = itemId;
    }

    public void Unequip(string characterId, string slot)
    {
        if (EquippedItems.TryGetValue(characterId, out var slots))
            slots.Remove(slot);
    }

    public void AddItem(string itemId, int count = 1)
    {
        if (Inventory.ContainsKey(itemId))
            Inventory[itemId] += count;
        else
            Inventory[itemId] = count;
    }

    public bool RemoveItem(string itemId, int count = 1)
    {
        if (!Inventory.TryGetValue(itemId, out int current) || current < count)
            return false;
        current -= count;
        if (current <= 0)
            Inventory.Remove(itemId);
        else
            Inventory[itemId] = current;
        return true;
    }

    /// <summary>Returns inventory sorted by item ID for consistent display.</summary>
    public List<KeyValuePair<string, int>> GetSortedInventory()
        => Inventory.OrderBy(kv => kv.Key).ToList();
}
