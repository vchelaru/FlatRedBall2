using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public record DamageResult(int Amount, bool WasCritical, string? ElementMessage);

public class DamageCalculator
{
    private readonly Random _random;

    public DamageCalculator(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public DamageResult CalculatePhysical(CombatantState attacker, CombatantState defender, AbilityData ability)
    {
        float atkMult = attacker.GetEffectiveStatMultiplier();
        float defMult = defender.GetEffectiveStatMultiplier();
        float baseDamage = attacker.STR * 2 * atkMult - defender.DEF * defMult;
        return ApplyModifiers(baseDamage, attacker, defender, ability, 0.9f, 1.1f);
    }

    public DamageResult CalculateMagical(CombatantState attacker, CombatantState defender, AbilityData ability)
    {
        float atkMult = attacker.GetEffectiveStatMultiplier();
        float defMult = defender.GetEffectiveStatMultiplier();
        float baseDamage = attacker.MAG * 2 * atkMult - defender.RES * defMult;
        return ApplyModifiers(baseDamage, attacker, defender, ability, 0.9f, 1.1f);
    }

    /// <summary>
    /// Calculates damage for a pet ability that ignores defense (e.g., Overdrive).
    /// </summary>
    public DamageResult CalculateIgnoringDefense(CombatantState attacker, AbilityData ability)
    {
        float atkMult = attacker.GetEffectiveStatMultiplier();
        float baseDamage = attacker.STR * 2 * atkMult;
        float variance = 0.9f + (float)_random.NextDouble() * 0.2f;
        float damage = baseDamage * ability.Multiplier * variance;
        int finalDamage = Math.Max(1, (int)damage);
        return new DamageResult(finalDamage, false, null);
    }

    public DamageResult CalculateHealing(CombatantState caster, AbilityData ability)
    {
        float variance = 0.95f + (float)_random.NextDouble() * 0.1f; // 0.95 to 1.05
        int amount = Math.Max(1, (int)(caster.MAG * ability.Multiplier * variance));
        return new DamageResult(amount, false, null);
    }

    /// <summary>
    /// Calculates damage for an ability, dispatching to the correct formula based on DamageType.
    /// </summary>
    public DamageResult Calculate(CombatantState attacker, CombatantState defender, AbilityData ability)
    {
        return ability.DamageType switch
        {
            DamageType.Physical => CalculatePhysical(attacker, defender, ability),
            DamageType.Magical => CalculateMagical(attacker, defender, ability),
            DamageType.Healing => CalculateHealing(attacker, ability),
            _ => new DamageResult(0, false, null),
        };
    }

    private DamageResult ApplyModifiers(
        float baseDamage,
        CombatantState attacker,
        CombatantState defender,
        AbilityData ability,
        float varianceLow,
        float varianceHigh)
    {
        float variance = varianceLow + (float)_random.NextDouble() * (varianceHigh - varianceLow);
        float damage = baseDamage * ability.Multiplier * variance;

        // Element multiplier
        float elementMult = ElementSystem.GetMultiplier(ability.Element, defender.ElementAffinities);
        damage *= elementMult;

        // Element resistance from status effects
        foreach (var effect in defender.StatusEffects)
        {
            if (effect.Type == StatusEffectType.ElementResist && effect.Element == ability.Element)
                damage *= effect.ResistMultiplier;
        }

        string? elementMessage = elementMult switch
        {
            > 1.0f => "Super effective!",
            < 1.0f and > 0f => "Resisted...",
            _ => null,
        };

        // Critical hit: LCK% chance, 1.5x
        bool isCrit = _random.Next(100) < attacker.LCK;
        if (isCrit)
            damage *= 1.5f;

        // Defending halves damage
        if (defender.IsDefending)
            damage *= 0.5f;

        int finalDamage = Math.Max(1, (int)damage);
        return new DamageResult(finalDamage, isCrit, elementMessage);
    }
}
