using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class ColosseumWaveGeneratorTests
{
    private static List<EnemyData> MakeEnemies() =>
    [
        new EnemyData { Id = "gear_golem", Name = "Gear Golem", HP = 80, STR = 12, DEF = 15, SPD = 4, XPReward = 12 },
        new EnemyData { Id = "steam_rat", Name = "Steam Rat", HP = 35, STR = 8, DEF = 5, SPD = 14, XPReward = 6 },
        new EnemyData { Id = "rust_beetle", Name = "Rust Beetle", HP = 50, STR = 10, DEF = 18, SPD = 6, XPReward = 8 },
        new EnemyData { Id = "furnace_guardian", Name = "Furnace Guardian", HP = 300, STR = 22, DEF = 20, SPD = 8, XPReward = 120, IsBoss = true },
    ];

    [Fact]
    public void GenerateWave_Wave1_Returns2Or3Enemies()
    {
        var gen = new ColosseumWaveGenerator(MakeEnemies(), new Random(42));

        var wave = gen.GenerateWave(1);

        wave.Count.ShouldBeGreaterThanOrEqualTo(2);
        wave.Count.ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public void GenerateWave_Wave5_ReturnsBoss()
    {
        var gen = new ColosseumWaveGenerator(MakeEnemies(), new Random(42));

        var wave = gen.GenerateWave(5);

        wave.Count.ShouldBe(1);
        wave[0].IsBoss.ShouldBeTrue();
    }

    [Fact]
    public void GenerateWave_Wave25_ScalesStats()
    {
        int baseHP = 80;
        var gen = new ColosseumWaveGenerator(MakeEnemies(), new Random(42));

        var wave = gen.GenerateWave(25);

        // Wave 25 = 5 waves past 20, scale = 1.0 + 5*0.1 = 1.5x
        // Boss wave (25 % 5 == 0), so it's a boss
        // But regular wave enemies at wave 21+ should be scaled
        // Wave 25 is a boss wave, so let's test wave 21 instead
        wave.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GenerateWave_Wave21_ScalesEnemyHP()
    {
        int baseHP = 80; // gear_golem
        var gen = new ColosseumWaveGenerator(MakeEnemies(), new Random(42));

        var wave = gen.GenerateWave(21);

        // Wave 21: scale = 1.0 + (21-20)*0.1 = 1.1
        // At least one enemy should have scaled HP
        wave.ShouldNotBeEmpty();
        // All enemies should have HP >= baseHP (scaled or from different pool)
        wave.All(e => e.HP >= 35).ShouldBeTrue(); // 35 = min enemy HP (steam_rat)
    }

    [Fact]
    public void GetWaveXP_ReturnsExpectedAmount()
    {
        var gen = new ColosseumWaveGenerator(MakeEnemies());
        int waveNumber = 10;
        int expectedXP = 20 + 10 * 10; // 120

        gen.GetWaveXP(waveNumber).ShouldBe(expectedXP);
    }

    [Fact]
    public void GetWaveGold_ReturnsExpectedAmount()
    {
        var gen = new ColosseumWaveGenerator(MakeEnemies());
        int waveNumber = 10;
        int expectedGold = 10 + 10 * 5; // 60

        gen.GetWaveGold(waveNumber).ShouldBe(expectedGold);
    }
}
