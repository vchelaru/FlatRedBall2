using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class EnemyAITests
{
    private static Dictionary<string, AbilityData> MakeAbilities() => new()
    {
        ["bite"] = new AbilityData
        {
            Id = "bite", Name = "Bite", MPCost = 0,
            DamageType = DamageType.Physical, Multiplier = 1.0f,
            Element = Element.None, TargetType = TargetType.SingleEnemy
        },
        ["heal"] = new AbilityData
        {
            Id = "heal", Name = "Heal", MPCost = 5,
            DamageType = DamageType.Healing, Multiplier = 1.0f,
            Element = Element.None, TargetType = TargetType.SingleAlly
        },
    };

    [Fact]
    public void DecideAction_LowHP_HealsIfAble()
    {
        var abilities = MakeAbilities();
        var ai = new EnemyAI(abilities, new Random(42));
        var enemy = new CombatantState
        {
            Id = "goblin", CurrentHP = 5, MaxHP = 100, CurrentMP = 50, MaxMP = 50,
            STR = 10, AbilityIds = ["bite", "heal"]
        };
        var state = new BattleState
        {
            Enemies = [enemy],
            PlayerParty = [new CombatantState { Id = "hero", CurrentHP = 50, MaxHP = 50 }]
        };

        var action = ai.DecideAction(enemy, state);

        action.AbilityId.ShouldBe("heal");
        action.TargetIds.ShouldContain("goblin");
    }

    [Fact]
    public void DecideAction_HealthyEnemy_AttacksPlayer()
    {
        var abilities = MakeAbilities();
        var ai = new EnemyAI(abilities, new Random(42));
        var enemy = new CombatantState
        {
            Id = "goblin", CurrentHP = 100, MaxHP = 100, CurrentMP = 50, MaxMP = 50,
            STR = 10, AbilityIds = ["bite", "heal"]
        };
        var state = new BattleState
        {
            Enemies = [enemy],
            PlayerParty = [new CombatantState { Id = "hero", CurrentHP = 50, MaxHP = 50 }]
        };

        var action = ai.DecideAction(enemy, state);

        action.AbilityId.ShouldBe("bite");
        action.TargetIds.ShouldContain("hero");
    }
}
