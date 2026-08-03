using RiftboundSample.Models;

namespace RiftboundSample.Systems;

/// <summary>
/// Manages multi-phase boss encounters. Tracks the current phase and applies
/// stat changes and ability swaps on phase transitions.
/// </summary>
public class BossPhaseSystem
{
    private static readonly string[] Phase1Abilities = ["attack", "shadow_decree", "void_lance", "dark_barrier"];
    private static readonly string[] Phase2Abilities = ["attack", "shadow_decree", "void_lance", "rift_distortion", "summon_minion", "gravity_well"];
    private static readonly string[] Phase3Abilities = ["attack", "void_lance", "gravity_well", "annihilation_wave", "time_stop", "oblivion"];

    public int CurrentPhase { get; private set; } = 1;

    /// <summary>
    /// The element absorbed in Phase 2 (random). Attacks of this element heal the boss instead.
    /// </summary>
    public Element? AbsorbedElement { get; private set; }

    /// <summary>
    /// Checks whether the boss should transition to a new phase based on current HP.
    /// Returns a list of events describing the transition (empty if no transition).
    /// </summary>
    public List<BattleEvent> CheckPhaseTransition(CombatantState boss)
    {
        var events = new List<BattleEvent>();
        float hpPercent = (float)boss.CurrentHP / boss.MaxHP;

        if (CurrentPhase == 1 && hpPercent <= 0.66f)
        {
            CurrentPhase = 2;
            ApplyPhaseEffects(boss, 2);
            events.Add(new BossPhaseChangeEvent(
                boss.Id,
                2,
                $"{boss.Name} absorbs the rift's power! Phase 2 begins!"));
        }
        else if (CurrentPhase == 2 && hpPercent <= 0.33f)
        {
            CurrentPhase = 3;
            ApplyPhaseEffects(boss, 3);
            events.Add(new BossPhaseChangeEvent(
                boss.Id,
                3,
                $"{boss.Name} enters a desperate rage! Phase 3 begins!"));
        }

        return events;
    }

    /// <summary>
    /// Applies stat modifications and ability swaps for the given phase.
    /// </summary>
    public void ApplyPhaseEffects(CombatantState boss, int phase)
    {
        switch (phase)
        {
            case 2:
                // Absorb a random element
                var elements = new[] { Element.Fire, Element.Ice, Element.Lightning, Element.Steam };
                AbsorbedElement = elements[Random.Shared.Next(elements.Length)];

                // Add element absorb (0.0 multiplier = immune/absorb)
                boss.ElementAffinities.Add(new ElementAffinity
                {
                    Element = AbsorbedElement.Value,
                    Multiplier = 0.0f,
                });

                boss.AbilityIds = new List<string>(Phase2Abilities);
                break;

            case 3:
                // +25% all stats
                boss.STR = (int)(boss.STR * 1.25f);
                boss.MAG = (int)(boss.MAG * 1.25f);
                boss.DEF = (int)(boss.DEF * 1.25f);
                boss.RES = (int)(boss.RES * 1.25f);
                boss.SPD = (int)(boss.SPD * 1.25f);

                boss.AbilityIds = new List<string>(Phase3Abilities);
                break;
        }
    }

    public void Reset()
    {
        CurrentPhase = 1;
        AbsorbedElement = null;
    }
}
