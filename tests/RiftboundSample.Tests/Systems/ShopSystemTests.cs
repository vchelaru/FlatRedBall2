using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class ShopSystemTests
{
    private static (ShopSystem shop, PartyState party) MakeShopAndParty()
    {
        var equipment = new Dictionary<string, EquipmentData>
        {
            ["sword_a"] = new() { Id = "sword_a", Slot = "weapon", StatBonuses = new() { ["STR"] = 5 } },
            ["sword_b"] = new() { Id = "sword_b", Slot = "weapon", StatBonuses = new() { ["STR"] = 8 } },
        };

        var shop = new ShopSystem(equipment);
        // Manually set stock since LoadShop needs a file
        shop.SetStock([
            new ShopItem { ItemId = "sword_a", Name = "Sword A", BuyPrice = 100, SellPrice = 50 },
            new ShopItem { ItemId = "sword_b", Name = "Sword B", BuyPrice = 200, SellPrice = 100 },
            new ShopItem { ItemId = "potion", Name = "Potion", BuyPrice = 30, SellPrice = 15 },
        ]);

        var party = new PartyState { Gold = 300 };
        return (shop, party);
    }

    [Fact]
    public void Buy_DeductsGoldAndAddsItem()
    {
        var (shop, party) = MakeShopAndParty();
        int initialGold = 300;
        int buyPrice = 100;

        bool success = shop.Buy(party, shop.CurrentStock[0]);

        success.ShouldBeTrue();
        party.Gold.ShouldBe(initialGold - buyPrice);
        party.Inventory["sword_a"].ShouldBe(1);
    }

    [Fact]
    public void Buy_InsufficientGold_ReturnsFalse()
    {
        var (shop, party) = MakeShopAndParty();
        party.Gold = 10;

        bool success = shop.Buy(party, shop.CurrentStock[1]); // 200g sword

        success.ShouldBeFalse();
        party.Gold.ShouldBe(10);
    }

    [Fact]
    public void CompareEquipment_ShowsStatDifference()
    {
        var (shop, party) = MakeShopAndParty();
        string characterId = "kael";

        // Equip sword_a first
        party.Equip(characterId, "weapon", "sword_a");

        // Compare with sword_b
        var diff = shop.CompareEquipment(party, characterId, "sword_b");

        diff["STR"].ShouldBe(3); // 8 - 5 = +3
    }

    [Fact]
    public void Sell_AddsGoldAndRemovesItem()
    {
        var (shop, party) = MakeShopAndParty();
        int initialGold = 300;
        int sellPrice = 50;
        party.AddItem("sword_a");

        bool success = shop.Sell(party, "sword_a");

        success.ShouldBeTrue();
        party.Gold.ShouldBe(initialGold + sellPrice);
        party.Inventory.ContainsKey("sword_a").ShouldBeFalse();
    }
}
