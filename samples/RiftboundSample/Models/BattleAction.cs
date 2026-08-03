namespace RiftboundSample.Models;

public record BattleAction(string CombatantId, string AbilityId, List<string> TargetIds);
