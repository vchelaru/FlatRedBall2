using RiftboundSample.Models;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Models;

public class PartyStateTests
{
    [Fact]
    public void AddItem_NewItem_SetsCountToOne()
    {
        var party = new PartyState();

        party.AddItem("potion");

        party.Inventory["potion"].ShouldBe(1);
    }

    [Fact]
    public void AddItem_ExistingItem_IncrementsCount()
    {
        var party = new PartyState();
        party.AddItem("potion", 3);

        party.AddItem("potion", 2);

        party.Inventory["potion"].ShouldBe(5);
    }

    [Fact]
    public void Equip_SetsSlotForCharacter()
    {
        var party = new PartyState();
        string characterId = "kael";
        string slot = "weapon";
        string itemId = "iron_wrench";

        party.Equip(characterId, slot, itemId);

        party.GetEquipped(characterId, slot).ShouldBe(itemId);
    }

    [Fact]
    public void GetEquipped_NoEquipment_ReturnsNull()
    {
        var party = new PartyState();

        party.GetEquipped("kael", "weapon").ShouldBeNull();
    }

    [Fact]
    public void RemoveItem_InsufficientCount_ReturnsFalse()
    {
        var party = new PartyState();
        party.AddItem("potion", 1);

        bool result = party.RemoveItem("potion", 2);

        result.ShouldBeFalse();
        party.Inventory["potion"].ShouldBe(1);
    }

    [Fact]
    public void RemoveItem_ExactCount_RemovesKeyEntirely()
    {
        var party = new PartyState();
        party.AddItem("potion", 2);

        bool result = party.RemoveItem("potion", 2);

        result.ShouldBeTrue();
        party.Inventory.ContainsKey("potion").ShouldBeFalse();
    }

    [Fact]
    public void Unequip_RemovesSlot()
    {
        var party = new PartyState();
        party.Equip("kael", "weapon", "iron_wrench");

        party.Unequip("kael", "weapon");

        party.GetEquipped("kael", "weapon").ShouldBeNull();
    }
}
