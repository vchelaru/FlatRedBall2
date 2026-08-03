using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class ProgressionSystemTests
{
    [Fact]
    public void XPForLevel_Level1_ReturnsZero()
    {
        ProgressionSystem.XPForLevel(1).ShouldBe(0);
    }

    [Fact]
    public void XPForLevel_Level2_Returns300()
    {
        // 100 * 2 * 3 / 2 = 300
        int expected = 300;
        ProgressionSystem.XPForLevel(2).ShouldBe(expected);
    }

    [Fact]
    public void XPForLevel_Level10_Returns5500()
    {
        // 100 * 10 * 11 / 2 = 5500
        int expected = 5500;
        ProgressionSystem.XPForLevel(10).ShouldBe(expected);
    }

    [Fact]
    public void XPForLevel_Level50_Returns127500()
    {
        // 100 * 50 * 51 / 2 = 127500
        int expected = 127500;
        ProgressionSystem.XPForLevel(50).ShouldBe(expected);
    }

    [Fact]
    public void XPToNextLevel_Level1_Returns300()
    {
        // XPForLevel(2) - XPForLevel(1) = 300 - 0 = 300
        int expected = 300;
        ProgressionSystem.XPToNextLevel(1).ShouldBe(expected);
    }

    [Fact]
    public void ApplyLevelUp_IncreasesStatsByGrowthRates()
    {
        int startHP = 100;
        int growthHP = 10;
        int newLevel = 3;
        var data = new CharacterData
        {
            HP = startHP,
            MP = 50,
            Level = 1,
            Growth = new GrowthRates { HP = growthHP, MP = 5, STR = 2, MAG = 1, DEF = 2, RES = 1, SPD = 1, LCK = 1 },
        };

        ProgressionSystem.ApplyLevelUp(data, newLevel);

        // 2 levels gained * 10 HP per level = 20 HP added
        data.HP.ShouldBe(startHP + growthHP * 2);
        data.Level.ShouldBe(newLevel);
    }

    [Fact]
    public void ApplyLevelUp_NullGrowth_NoChange()
    {
        int startHP = 100;
        var data = new CharacterData { HP = startHP, Level = 1, Growth = null };

        ProgressionSystem.ApplyLevelUp(data, 5);

        data.HP.ShouldBe(startHP);
        data.Level.ShouldBe(1);
    }

    [Fact]
    public void AddXP_CausesLevelUp_ReturnsLevelsGained()
    {
        var data = new CharacterData
        {
            Level = 1,
            XP = 0,
            XPToNextLevel = 300,
            HP = 100,
            Growth = new GrowthRates { HP = 10, MP = 5, STR = 2, MAG = 1, DEF = 2, RES = 1, SPD = 1, LCK = 1 },
        };

        int levelsGained = ProgressionSystem.AddXP(data, 300);

        levelsGained.ShouldBe(1);
        data.Level.ShouldBe(2);
    }
}
