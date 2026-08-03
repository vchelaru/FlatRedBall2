using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class DamageCalculatorTests
{
    [Fact]
    public void CalculatePhysical_BasicDamage_ReturnsExpectedRange()
    {
        var str = 20;
        var def = 10;
        var multiplier = 1.0f;
        // base = (20*2 - 10) * 1.0 = 30, variance 0.9-1.1 → 27-33
        var attacker = new CombatantState { STR = str, LCK = 0, CurrentHP = 1, MaxHP = 1 };
        var defender = new CombatantState { DEF = def, CurrentHP = 1, MaxHP = 1 };
        var ability = new AbilityData { Multiplier = multiplier, DamageType = DamageType.Physical, Element = Element.None };
        var calc = new DamageCalculator(new Random(42));

        var result = calc.CalculatePhysical(attacker, defender, ability);

        result.Amount.ShouldBeInRange(27, 33);
    }

    [Fact]
    public void CalculatePhysical_MinimumDamageIsOne()
    {
        // STR=1, DEF=999 → base = (2-999) = negative → clamped to 1
        var attacker = new CombatantState { STR = 1, LCK = 0, CurrentHP = 1, MaxHP = 1 };
        var defender = new CombatantState { DEF = 999, CurrentHP = 1, MaxHP = 1 };
        var ability = new AbilityData { Multiplier = 1.0f, DamageType = DamageType.Physical, Element = Element.None };
        var calc = new DamageCalculator(new Random(42));

        var result = calc.CalculatePhysical(attacker, defender, ability);

        result.Amount.ShouldBe(1);
    }

    [Fact]
    public void CalculateHealing_ScalesWithMAG()
    {
        var mag = 30;
        var multiplier = 1.5f;
        // base = 30 * 1.5 = 45, variance 0.95-1.05 → ~42-47
        var caster = new CombatantState { MAG = mag, CurrentHP = 1, MaxHP = 1 };
        var ability = new AbilityData { Multiplier = multiplier, DamageType = DamageType.Healing };
        var calc = new DamageCalculator(new Random(42));

        var result = calc.CalculateHealing(caster, ability);

        result.Amount.ShouldBeInRange(42, 48);
        result.WasCritical.ShouldBeFalse();
    }
}
