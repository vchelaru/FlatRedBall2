using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class ATBSystem
{
    private readonly List<CombatantState> _combatants = [];
    private bool _paused;

    /// <summary>
    /// Speed multiplier applied to all gauge fill rates (1x, 2x, 4x).
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    public void AddCombatant(CombatantState combatant)
    {
        _combatants.Add(combatant);
    }

    public void RemoveCombatant(string combatantId)
    {
        _combatants.RemoveAll(c => c.Id == combatantId);
    }

    public void SetPaused(bool paused) => _paused = paused;

    /// <summary>
    /// Advances all living combatants' ATB gauges.
    /// Fill rate per second = SPD / 100.0, scaled by SpeedMultiplier.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        if (_paused || deltaSeconds <= 0)
            return;

        foreach (var c in _combatants)
        {
            if (!c.IsAlive || c.IsATBFull)
                continue;

            float fillRate = c.SPD / 100f * SpeedMultiplier;
            c.ATB = Math.Min(c.ATB + fillRate * deltaSeconds, 2f); // Cap at 2 to preserve overflow ordering
        }
    }

    /// <summary>
    /// Returns combatants whose gauge >= 1.0, ordered by descending overflow (highest first).
    /// </summary>
    public List<CombatantState> GetReadyCombatants()
    {
        return _combatants
            .Where(c => c.IsAlive && c.IsATBFull)
            .OrderByDescending(c => c.ATB)
            .ToList();
    }

    public void ResetGauge(string combatantId)
    {
        var c = _combatants.FirstOrDefault(c => c.Id == combatantId);
        if (c != null)
            c.ATB = 0f;
    }

    /// <summary>
    /// Sets a combatant's gauge to a negative value so it fills normally from there.
    /// The combatant is never skipped — they just take longer to reach 1.0.
    /// </summary>
    public void ApplySurpriseDelay(string combatantId, float delaySeconds)
    {
        var c = _combatants.FirstOrDefault(c => c.Id == combatantId);
        if (c == null) return;

        float fillRate = c.SPD / 100f;
        if (fillRate <= 0) return;

        c.ATB = -(fillRate * delaySeconds);
    }
}
