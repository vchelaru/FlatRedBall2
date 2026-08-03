using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class BestiaryRewardTests
{
    private static BestiarySystem MakeSystem(int enemyCount = 10)
    {
        var enemies = Enumerable.Range(0, enemyCount)
            .Select(i => new EnemyData { Id = $"enemy_{i}", Name = $"Enemy {i}", HP = 100 })
            .ToList();
        var system = new BestiarySystem(enemies);
        system.SetRewards(
        [
            new BestiaryReward { RequiredEntries = 3, RewardType = "gold", RewardId = "gold", RewardCount = 500, Description = "3 entries: 500 gold" },
            new BestiaryReward { RequiredEntries = 8, RewardType = "item", RewardId = "rare_ore", RewardCount = 2, Description = "8 entries: rare ore" },
        ]);
        return system;
    }

    [Fact]
    public void CheckRewards_NotEnoughEntries_ReturnsEmpty()
    {
        var system = MakeSystem();
        // No enemies recorded

        system.CheckRewards().ShouldBeEmpty();
    }

    [Fact]
    public void CheckRewards_EnoughEntries_ReturnsAvailableReward()
    {
        int requiredEntries = 3;
        var system = MakeSystem();

        // Record enough enemies to meet first threshold
        for (int i = 0; i < requiredEntries; i++)
            system.RecordDefeat($"enemy_{i}");

        var rewards = system.CheckRewards();
        rewards.Count.ShouldBe(1);
        rewards[0].Reward.RewardCount.ShouldBe(500);
    }

    [Fact]
    public void ClaimReward_ValidIndex_ReturnsTrue()
    {
        var system = MakeSystem();
        for (int i = 0; i < 3; i++)
            system.RecordDefeat($"enemy_{i}");

        system.ClaimReward(0).ShouldBeTrue();
    }

    [Fact]
    public void ClaimReward_AlreadyClaimed_ReturnsFalse()
    {
        var system = MakeSystem();
        for (int i = 0; i < 3; i++)
            system.RecordDefeat($"enemy_{i}");

        system.ClaimReward(0);

        system.ClaimReward(0).ShouldBeFalse();
    }

    [Fact]
    public void ClaimReward_NotEnoughEntries_ReturnsFalse()
    {
        var system = MakeSystem();

        system.ClaimReward(0).ShouldBeFalse();
    }

    [Fact]
    public void CheckRewards_ExcludesAlreadyClaimed()
    {
        var system = MakeSystem();
        for (int i = 0; i < 3; i++)
            system.RecordDefeat($"enemy_{i}");

        system.ClaimReward(0);

        system.CheckRewards().ShouldBeEmpty();
    }
}
