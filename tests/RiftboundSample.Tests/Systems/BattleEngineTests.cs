using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class BattleEngineTests
{
    private static Dictionary<string, AbilityData> MakeAbilities() => new()
    {
        ["slash"] = new AbilityData
        {
            Id = "slash", Name = "Slash", MPCost = 0,
            DamageType = DamageType.Physical, Multiplier = 1.0f, Element = Element.None,
            TargetType = TargetType.SingleEnemy
        },
        ["heal"] = new AbilityData
        {
            Id = "heal", Name = "Heal", MPCost = 5,
            DamageType = DamageType.Healing, Multiplier = 1.0f, Element = Element.None,
            TargetType = TargetType.SingleAlly
        },
    };

    private static BattleState MakeState()
    {
        return new BattleState
        {
            PlayerParty =
            [
                new CombatantState
                {
                    Id = "hero", Name = "Hero", IsPlayer = true,
                    CurrentHP = 100, MaxHP = 100, CurrentMP = 50, MaxMP = 50,
                    STR = 20, MAG = 15, DEF = 10, RES = 10, SPD = 50, LCK = 0,
                    AbilityIds = ["slash", "heal"]
                }
            ],
            Enemies =
            [
                new CombatantState
                {
                    Id = "goblin", Name = "Goblin", IsPlayer = false,
                    CurrentHP = 30, MaxHP = 30, CurrentMP = 10, MaxMP = 10,
                    STR = 10, MAG = 5, DEF = 5, RES = 5, SPD = 40,
                    AbilityIds = ["slash"]
                }
            ]
        };
    }

    [Fact]
    public void ExecuteAction_PlayerSlashesEnemy_DealsDamage()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        engine.Initialize(state);

        var action = new BattleAction("hero", "slash", ["goblin"]);
        var events = engine.ExecuteAction(action);

        events.ShouldContain(e => e is DamageEvent);
        var dmg = events.OfType<DamageEvent>().First();
        dmg.AttackerId.ShouldBe("hero");
        dmg.TargetId.ShouldBe("goblin");
        dmg.Damage.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ExecuteAction_DeadTarget_RetargetsToLivingEnemy()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        // Add a second enemy; kill the first one
        state.Enemies.Add(new CombatantState
        {
            Id = "orc", Name = "Orc", IsPlayer = false,
            CurrentHP = 50, MaxHP = 50, CurrentMP = 10, MaxMP = 10,
            STR = 12, DEF = 8, RES = 5, SPD = 30,
            AbilityIds = ["slash"]
        });
        state.Enemies[0].CurrentHP = 0; // goblin is dead
        engine.Initialize(state);

        var action = new BattleAction("hero", "slash", ["goblin"]);
        var events = engine.ExecuteAction(action);

        // Should retarget to orc since goblin is dead
        var dmg = events.OfType<DamageEvent>().ShouldHaveSingleItem();
        dmg.TargetId.ShouldBe("orc");
    }

    [Fact]
    public void ExecuteAction_KillEnemy_ProducesDeathAndVictory()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        state.Enemies[0].CurrentHP = 1; // one hit will kill
        engine.Initialize(state);

        var action = new BattleAction("hero", "slash", ["goblin"]);
        var events = engine.ExecuteAction(action);

        events.OfType<DeathEvent>().ShouldContain(d => d.CombatantId == "goblin");
        state.IsVictory.ShouldBeTrue();
    }

    [Fact]
    public void ExecuteAction_MPRegen_RestoresOnePercentAfterActing()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        int expectedMaxMP = 50;
        int startingMP = 40;
        state.PlayerParty[0].CurrentMP = startingMP;
        state.PlayerParty[0].MaxMP = expectedMaxMP;
        engine.Initialize(state);

        // Slash costs 0 MP, so after acting MP should increase by ceil(50*0.01) = 1
        engine.ExecuteAction(new BattleAction("hero", "slash", ["goblin"]));

        int expectedRegen = 1;
        state.PlayerParty[0].CurrentMP.ShouldBe(startingMP + expectedRegen);
    }

    [Fact]
    public void ExecuteAction_Overkill_SplashesDamageToSecondEnemy()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        state.Enemies[0].CurrentHP = 1; // will die in one hit, causing overkill
        state.Enemies.Add(new CombatantState
        {
            Id = "orc", Name = "Orc", IsPlayer = false,
            CurrentHP = 100, MaxHP = 100, CurrentMP = 10, MaxMP = 10,
            STR = 12, DEF = 8, RES = 5, SPD = 30,
            AbilityIds = ["slash"]
        });
        engine.Initialize(state);

        int orcHpBefore = 100;
        engine.ExecuteAction(new BattleAction("hero", "slash", ["goblin"]));

        // Goblin should be dead
        state.Enemies[0].IsAlive.ShouldBeFalse();
        // Orc should have taken splash damage (excess * 50%)
        var splash = state.Enemies[1];
        splash.CurrentHP.ShouldBeLessThan(orcHpBefore);
    }

    [Fact]
    public void FillLimitGaugeFromDamage_PlayerTakesDamage_GaugeIncreases()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        string limitAbilityId = "test_limit";
        state.PlayerParty[0].LimitBreakAbilityId = limitAbilityId;
        state.PlayerParty[0].MaxHP = 100;
        engine.Initialize(state);

        // Taking 50 damage should fill gauge by 50/100 * 0.5 = 0.25
        int damage = 50;
        float expectedFill = 0.25f;
        engine.FillLimitGaugeFromDamage("hero", damage);

        state.PlayerParty[0].LimitGauge.ShouldBe(expectedFill, tolerance: 0.001f);
    }

    [Fact]
    public void FillLimitGaugeFromAllyDeath_AllyDies_OtherAlliesGainGauge()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        string limitAbilityId = "test_limit";
        state.PlayerParty.Add(new CombatantState
        {
            Id = "ally", Name = "Ally", IsPlayer = true,
            CurrentHP = 100, MaxHP = 100, CurrentMP = 50, MaxMP = 50,
            STR = 10, DEF = 10, SPD = 10,
            LimitBreakAbilityId = limitAbilityId,
        });
        engine.Initialize(state);

        float expectedFill = 0.25f;
        engine.FillLimitGaugeFromAllyDeath("hero");

        // Ally (not the dead one) should gain 0.25
        state.PlayerParty[1].LimitGauge.ShouldBe(expectedFill, tolerance: 0.001f);
    }

    [Fact]
    public void ExecuteAction_LimitBreak_ConsumesGauge()
    {
        var limitAbility = new AbilityData
        {
            Id = "test_limit", Name = "Limit", MPCost = 0,
            DamageType = DamageType.Physical, Multiplier = 3.0f, Element = Element.None,
            TargetType = TargetType.AllEnemies
        };
        var abilities = MakeAbilities();
        abilities["test_limit"] = limitAbility;

        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        state.PlayerParty[0].LimitBreakAbilityId = "test_limit";
        state.PlayerParty[0].LimitGauge = 1.0f;
        engine.Initialize(state);

        engine.ExecuteAction(new BattleAction("hero", "test_limit", ["goblin"]));

        // Gauge should be consumed
        state.PlayerParty[0].LimitGauge.ShouldBe(0f);
    }

    [Fact]
    public void ProcessStatusEffects_DamageOverTime_DealsDamage()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        int dotAmount = 10;
        int startingHP = 100;
        state.PlayerParty[0].CurrentHP = startingHP;
        state.PlayerParty[0].StatusEffects.Add(new StatusEffect
        {
            Name = "Poison",
            Type = StatusEffectType.DamageOverTime,
            Amount = dotAmount,
            RemainingTurns = 3
        });
        engine.Initialize(state);

        var events = engine.ProcessStatusEffects(state.PlayerParty[0]);

        state.PlayerParty[0].CurrentHP.ShouldBe(startingHP - dotAmount);
        events.ShouldContain(e => e is StatusTickEvent);
    }

    [Fact]
    public void ProcessStatusEffects_Stun_EmitsStunEvent()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        state.Enemies[0].StatusEffects.Add(new StatusEffect
        {
            Name = "Stun",
            Type = StatusEffectType.Stun,
            RemainingTurns = 1
        });
        engine.Initialize(state);

        var events = engine.ProcessStatusEffects(state.Enemies[0]);

        events.ShouldContain(e => e is StunEvent);
        // After processing, the stun effect should be removed (1 turn)
        state.Enemies[0].StatusEffects.ShouldBeEmpty();
    }

    [Fact]
    public void SubmitPlayerAction_ProcessedOnNextUpdate()
    {
        var abilities = MakeAbilities();
        var engine = new BattleEngine(abilities, new Random(42));
        var state = MakeState();
        engine.Initialize(state);

        // Manually set hero ATB to full so the action can execute
        state.PlayerParty[0].ATB = 1.0f;

        engine.SubmitPlayerAction(new BattleAction("hero", "slash", ["goblin"]));
        var events = engine.Update(0f);

        events.ShouldContain(e => e is DamageEvent);
    }
}
