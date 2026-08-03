using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class EnemyAI
{
    private readonly Random _random;
    private readonly Dictionary<string, AbilityData> _abilityLookup;

    /// <summary>
    /// HP threshold percentages at which bosses use telegraph abilities (checked high to low).
    /// </summary>
    private static readonly float[] BossThresholds = [0.75f, 0.50f, 0.25f];

    public EnemyAI(Dictionary<string, AbilityData> abilityLookup, Random? random = null)
    {
        _abilityLookup = abilityLookup;
        _random = random ?? Random.Shared;
    }

    /// <summary>
    /// Decides an action for the given enemy based on the current battle state.
    /// </summary>
    public BattleAction DecideAction(CombatantState enemy, BattleState state)
    {
        var abilities = GetAvailableAbilities(enemy);
        var healingAbilities = abilities.Where(a => a.DamageType == DamageType.Healing).ToList();
        var offensiveAbilities = abilities.Where(a =>
            a.DamageType == DamageType.Physical || a.DamageType == DamageType.Magical).ToList();

        float hpPercent = (float)enemy.CurrentHP / enemy.MaxHP;
        var allies = state.Enemies.Where(e => e.IsAlive).ToList();
        var playerTargets = state.PlayerParty.Where(p => p.IsAlive).ToList();

        // Boss telegraph at HP thresholds
        if (state.IsBossBattle && offensiveAbilities.Count > 0)
        {
            foreach (float threshold in BossThresholds)
            {
                if (hpPercent <= threshold)
                {
                    // Use strongest ability (highest multiplier)
                    var strongest = offensiveAbilities.OrderByDescending(a => a.Multiplier).First();
                    var targets = SelectTargets(strongest, playerTargets);
                    return new BattleAction(enemy.Id, strongest.Id, targets);
                }
            }
        }

        // Self-heal if HP < 30%
        if (hpPercent < 0.3f && healingAbilities.Count > 0)
        {
            var heal = healingAbilities[_random.Next(healingAbilities.Count)];
            return new BattleAction(enemy.Id, heal.Id, [enemy.Id]);
        }

        // Heal ally if any ally HP < 20%
        var woundedAlly = allies.FirstOrDefault(a => a.Id != enemy.Id && (float)a.CurrentHP / a.MaxHP < 0.2f);
        if (woundedAlly != null && healingAbilities.Count > 0)
        {
            var heal = healingAbilities[_random.Next(healingAbilities.Count)];
            return new BattleAction(enemy.Id, heal.Id, [woundedAlly.Id]);
        }

        // Default: random offensive ability
        if (offensiveAbilities.Count > 0 && playerTargets.Count > 0)
        {
            var ability = offensiveAbilities[_random.Next(offensiveAbilities.Count)];
            var targets = SelectTargets(ability, playerTargets);
            return new BattleAction(enemy.Id, ability.Id, targets);
        }

        // Fallback: first ability, random target
        var fallback = abilities.First();
        var fallbackTargets = SelectTargets(fallback, playerTargets);
        return new BattleAction(enemy.Id, fallback.Id, fallbackTargets);
    }

    private List<string> SelectTargets(AbilityData ability, List<CombatantState> potentialTargets)
    {
        return ability.TargetType switch
        {
            TargetType.AllEnemies => potentialTargets.Select(t => t.Id).ToList(),
            TargetType.Self => [], // handled by caller
            TargetType.SingleAlly => [], // handled by caller
            TargetType.AllAllies => [], // handled by caller
            // SingleEnemy — target weakest (lowest HP)
            _ => [potentialTargets.OrderBy(t => t.CurrentHP).First().Id],
        };
    }

    private List<AbilityData> GetAvailableAbilities(CombatantState enemy)
    {
        return enemy.AbilityIds
            .Where(id => _abilityLookup.ContainsKey(id))
            .Select(id => _abilityLookup[id])
            .Where(a => a.MPCost <= enemy.CurrentMP)
            .ToList();
    }
}
