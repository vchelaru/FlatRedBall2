namespace RiftboundSample.Models;

/// <summary>
/// Runtime battle state for a single combatant (party member or enemy).
/// Created from CharacterData or EnemyData at the start of each battle.
/// </summary>
public class CombatantState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsPlayer { get; set; }

    // Current / max stats
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int CurrentMP { get; set; }
    public int MaxMP { get; set; }

    public int STR { get; set; }
    public int MAG { get; set; }
    public int DEF { get; set; }
    public int RES { get; set; }
    public int SPD { get; set; }
    public int LCK { get; set; }

    /// <summary>ATB gauge from 0.0 to 1.0. Combatant is ready to act when >= 1.0.</summary>
    public float ATB { get; set; }
    public bool IsATBFull => ATB >= 1.0f;

    public bool IsAlive => CurrentHP > 0;
    public bool IsDefending { get; set; }

    public RowPosition Row { get; set; } = RowPosition.Front;
    public List<string> AbilityIds { get; set; } = [];
    public List<ElementAffinity> ElementAffinities { get; set; } = [];
    public List<StatusEffect> StatusEffects { get; set; } = [];

    public bool IsBoss { get; set; }

    /// <summary>Last ability ID used by this combatant (for auto-battle and menu memory).</summary>
    public string? LastAction { get; set; }

    /// <summary>XP reward for defeating this combatant (enemies only).</summary>
    public int XPReward { get; set; }

    /// <summary>Pet gauge from 0.0 to 1.0 for player characters.</summary>
    public float PetGauge { get; set; }
    public string? PetId { get; set; }

    /// <summary>Limit gauge from 0.0 to 1.0. Fills from taking damage, ally death, and low HP.</summary>
    public float LimitGauge { get; set; }

    /// <summary>Limit break ability ID from CharacterData. Null if no limit break.</summary>
    public string? LimitBreakAbilityId { get; set; }

    /// <summary>
    /// Returns the combined stat multiplier from all active status effects.
    /// </summary>
    public float GetEffectiveStatMultiplier()
    {
        float multiplier = 1f;
        foreach (var effect in StatusEffects)
            multiplier *= effect.StatMultiplier;
        return multiplier;
    }

    public static CombatantState FromCharacter(CharacterData c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        IsPlayer = true,
        CurrentHP = c.HP,
        MaxHP = c.HP,
        CurrentMP = c.MP,
        MaxMP = c.MP,
        STR = c.STR,
        MAG = c.MAG,
        DEF = c.DEF,
        RES = c.RES,
        SPD = c.SPD,
        LCK = c.LCK,
        Row = c.Row,
        AbilityIds = new List<string>(c.AbilityIds),
        LimitBreakAbilityId = c.LimitBreakAbilityId,
    };

    public static CombatantState FromEnemy(EnemyData e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        IsPlayer = false,
        CurrentHP = e.HP,
        MaxHP = e.HP,
        CurrentMP = e.MP,
        MaxMP = e.MP,
        STR = e.STR,
        MAG = e.MAG,
        DEF = e.DEF,
        RES = e.RES,
        SPD = e.SPD,
        AbilityIds = new List<string>(e.AbilityIds),
        ElementAffinities = e.ElementAffinities.Select(a => new ElementAffinity
        {
            Element = a.Element,
            Multiplier = a.Multiplier
        }).ToList(),
        IsBoss = e.IsBoss,
        XPReward = e.XPReward,
    };
}
