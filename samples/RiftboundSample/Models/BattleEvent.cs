namespace RiftboundSample.Models;

public abstract record BattleEvent;

public record DamageEvent(
    string AttackerId,
    string TargetId,
    int Damage,
    bool WasCritical,
    string? ElementMessage) : BattleEvent;

public record HealEvent(
    string CasterId,
    string TargetId,
    int Amount) : BattleEvent;

public record DeathEvent(string CombatantId) : BattleEvent;

public record BattleEndEvent(bool PlayerVictory) : BattleEvent;

public record ActionEvent(
    string CombatantId,
    string AbilityId,
    List<string> TargetIds) : BattleEvent;

/// <summary>Emitted when a boss telegraphs an upcoming powerful attack.</summary>
public record TelegraphEvent(string BossId, string AbilityId) : BattleEvent;

/// <summary>Emitted on victory with XP distribution info.</summary>
public record VictoryEvent(int TotalXP, Dictionary<string, int> XPPerCombatant) : BattleEvent;

/// <summary>Emitted when overkill splash damage hits a secondary target.</summary>
public record OverkillSplashEvent(string AttackerId, string TargetId, int Damage) : BattleEvent;

public record PetGaugeFullEvent(string OwnerId, string PetName) : BattleEvent;

public record PetActionEvent(
    string OwnerId,
    string PetName,
    string AbilityId,
    int Damage) : BattleEvent;

/// <summary>Emitted when a combatant's limit gauge reaches 1.0.</summary>
public record LimitGaugeFullEvent(string CombatantId) : BattleEvent;

/// <summary>Emitted when a status effect ticks (DoT/HoT).</summary>
public record StatusTickEvent(string CombatantId, string EffectName, int Amount, bool IsHeal) : BattleEvent;

/// <summary>Emitted when a combatant is stunned and skips their turn.</summary>
public record StunEvent(string CombatantId) : BattleEvent;

/// <summary>Emitted when a shield absorbs damage.</summary>
public record ShieldAbsorbEvent(string CombatantId, int Absorbed, int Remaining) : BattleEvent;

/// <summary>Emitted when a counter-attack triggers.</summary>
public record CounterEvent(string CounterId, string AttackerId, int Damage) : BattleEvent;

/// <summary>Emitted when a multi-phase boss transitions to a new phase.</summary>
public record BossPhaseChangeEvent(string BossId, int NewPhase, string Message) : BattleEvent;
