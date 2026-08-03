using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class NewGamePlusSystemTests
{
    [Fact]
    public void CreateNewGamePlusSave_SetsNewGamePlusFlag()
    {
        var completedSave = new SaveData
        {
            CurrentMap = "the_fade",
            Flags = [],
            DiscoveredRecipes = ["bronze_sword", "iron_shield"],
        };

        var ngPlusSave = NewGamePlusSystem.CreateNewGamePlusSave(completedSave);

        ngPlusSave.Flags["new_game_plus"].ShouldBeTrue();
        ngPlusSave.Flags["ng_plus_unlocked"].ShouldBeTrue();
    }

    [Fact]
    public void CreateNewGamePlusSave_ResetsMapToBrasshollow()
    {
        string expectedMap = "brasshollow";
        var completedSave = new SaveData
        {
            CurrentMap = "the_fade",
            Flags = [],
            DiscoveredRecipes = ["bronze_sword"],
        };

        var ngPlusSave = NewGamePlusSystem.CreateNewGamePlusSave(completedSave);

        ngPlusSave.CurrentMap.ShouldBe(expectedMap);
    }

    [Fact]
    public void CreateNewGamePlusSave_PreservesDiscoveredRecipes()
    {
        var recipes = new List<string> { "bronze_sword", "iron_shield" };
        var completedSave = new SaveData
        {
            Flags = [],
            DiscoveredRecipes = recipes,
        };

        var ngPlusSave = NewGamePlusSystem.CreateNewGamePlusSave(completedSave);

        ngPlusSave.DiscoveredRecipes.ShouldBe(recipes);
    }

    [Fact]
    public void IsNewGamePlus_WithFlag_ReturnsTrue()
    {
        var data = new SaveData { Flags = new() { ["new_game_plus"] = true } };

        NewGamePlusSystem.IsNewGamePlus(data).ShouldBeTrue();
    }

    [Fact]
    public void IsNewGamePlus_WithoutFlag_ReturnsFalse()
    {
        var data = new SaveData { Flags = [] };

        NewGamePlusSystem.IsNewGamePlus(data).ShouldBeFalse();
    }

    [Fact]
    public void ScaleEnemy_MultipliesStatsByOnePointFive()
    {
        int originalHP = 100;
        int originalSTR = 20;
        int expectedHP = 150;
        int expectedSTR = 30;

        var original = new EnemyData
        {
            Id = "test",
            Name = "Test",
            HP = originalHP,
            MP = 50,
            STR = originalSTR,
            MAG = 10,
            DEF = 10,
            RES = 10,
            SPD = 10,
            XPReward = 100,
            AbilityIds = ["attack"],
            ElementAffinities = [],
            DropTable = [],
        };

        var scaled = NewGamePlusSystem.ScaleEnemy(original);

        scaled.HP.ShouldBe(expectedHP);
        scaled.STR.ShouldBe(expectedSTR);
        // Original should be unchanged
        original.HP.ShouldBe(originalHP);
    }
}
