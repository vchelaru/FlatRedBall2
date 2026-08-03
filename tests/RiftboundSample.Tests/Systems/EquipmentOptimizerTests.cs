using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class EquipmentOptimizerTests
{
    private static CharacterData MakeFighter() => new()
    {
        Id = "kael", Name = "Kael",
        STR = 14, MAG = 8, DEF = 12, RES = 6, SPD = 10, LCK = 7,
    };

    private static CharacterData MakeMage() => new()
    {
        Id = "mira", Name = "Mira",
        STR = 6, MAG = 16, DEF = 7, RES = 13, SPD = 12, LCK = 9,
    };

    private static List<EquipmentData> MakeEquipment() =>
    [
        new() { Id = "iron_sword", Slot = "weapon", StatBonuses = new() { ["STR"] = 10 } },
        new() { Id = "magic_staff", Slot = "weapon", StatBonuses = new() { ["MAG"] = 12 } },
        new() { Id = "iron_armor", Slot = "armor", StatBonuses = new() { ["DEF"] = 8 } },
        new() { Id = "silk_robe", Slot = "armor", StatBonuses = new() { ["RES"] = 10, ["MAG"] = 3 } },
    ];

    [Fact]
    public void GetOptimalEquipment_Fighter_PrefersStrengthWeapon()
    {
        var character = MakeFighter();
        string expectedWeapon = "iron_sword";
        var inventory = new Dictionary<string, int>
        {
            ["iron_sword"] = 1,
            ["magic_staff"] = 1,
            ["iron_armor"] = 1,
            ["silk_robe"] = 1,
        };

        var result = EquipmentOptimizer.GetOptimalEquipment(character, inventory, MakeEquipment());

        result["weapon"].ShouldBe(expectedWeapon);
    }

    [Fact]
    public void GetOptimalEquipment_Mage_PrefersMagicWeapon()
    {
        var character = MakeMage();
        string expectedWeapon = "magic_staff";
        var inventory = new Dictionary<string, int>
        {
            ["iron_sword"] = 1,
            ["magic_staff"] = 1,
            ["iron_armor"] = 1,
            ["silk_robe"] = 1,
        };

        var result = EquipmentOptimizer.GetOptimalEquipment(character, inventory, MakeEquipment());

        result["weapon"].ShouldBe(expectedWeapon);
    }

    [Fact]
    public void GetOptimalEquipment_OnlyConsidersInventoryItems()
    {
        var character = MakeFighter();
        // Only magic_staff in inventory, not iron_sword
        var inventory = new Dictionary<string, int>
        {
            ["magic_staff"] = 1,
        };

        var result = EquipmentOptimizer.GetOptimalEquipment(character, inventory, MakeEquipment());

        result["weapon"].ShouldBe("magic_staff");
        result.ContainsKey("armor").ShouldBeFalse();
    }
}
