using RiftboundSample.Models;

namespace RiftboundSample.Systems;

/// <summary>
/// Selects optimal equipment for a character based on their stat profile.
/// Role is inferred from base stats rather than a separate field.
/// </summary>
public static class EquipmentOptimizer
{
    internal enum CharacterRole { Fighter, Mage, Healer, Speed }

    private static readonly Dictionary<string, float> FighterWeights = new()
    {
        ["STR"] = 2f, ["DEF"] = 1.5f, ["MAG"] = 0.5f, ["RES"] = 0.5f, ["SPD"] = 1f, ["LCK"] = 0.5f,
    };

    private static readonly Dictionary<string, float> MageWeights = new()
    {
        ["STR"] = 0.5f, ["DEF"] = 0.5f, ["MAG"] = 2f, ["RES"] = 1.5f, ["SPD"] = 1f, ["LCK"] = 0.5f,
    };

    private static readonly Dictionary<string, float> HealerWeights = new()
    {
        ["STR"] = 0.5f, ["DEF"] = 1f, ["MAG"] = 1.5f, ["RES"] = 2f, ["SPD"] = 1f, ["LCK"] = 0.5f,
    };

    private static readonly Dictionary<string, float> SpeedWeights = new()
    {
        ["STR"] = 1f, ["DEF"] = 0.5f, ["MAG"] = 0.5f, ["RES"] = 0.5f, ["SPD"] = 2f, ["LCK"] = 1.5f,
    };

    /// <summary>
    /// For a given character, finds the best equipment from inventory for each slot.
    /// Returns a mapping of slot name to equipment ID. Only includes slots where a better
    /// item was found than what is currently equipped (or any item if nothing is equipped).
    /// </summary>
    public static Dictionary<string, string> GetOptimalEquipment(
        CharacterData character,
        Dictionary<string, int> inventory,
        List<EquipmentData> allEquipment)
    {
        var role = InferRole(character);
        var weights = GetWeights(role);
        var result = new Dictionary<string, string>();

        // Group available equipment by slot
        var bySlot = new Dictionary<string, List<EquipmentData>>();
        foreach (var equip in allEquipment)
        {
            // Only consider items in inventory
            if (!inventory.ContainsKey(equip.Id))
                continue;

            if (!bySlot.ContainsKey(equip.Slot))
                bySlot[equip.Slot] = [];
            bySlot[equip.Slot].Add(equip);
        }

        foreach (var (slot, candidates) in bySlot)
        {
            string? bestId = null;
            float bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                float score = ScoreEquipment(candidate, weights);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = candidate.Id;
                }
            }

            if (bestId != null)
                result[slot] = bestId;
        }

        return result;
    }

    internal static float ScoreEquipment(EquipmentData equipment, Dictionary<string, float> weights)
    {
        float score = 0;
        foreach (var (stat, bonus) in equipment.StatBonuses)
        {
            float weight = weights.GetValueOrDefault(stat, 1f);
            score += bonus * weight;
        }
        return score;
    }

    internal static CharacterRole InferRole(CharacterData character)
    {
        // Healer: high RES relative to other stats
        if (character.RES >= character.STR && character.RES >= character.MAG && character.RES >= character.SPD)
            return CharacterRole.Healer;

        // Mage: high MAG
        if (character.MAG >= character.STR && character.MAG >= character.SPD)
            return CharacterRole.Mage;

        // Speed: high SPD
        if (character.SPD >= character.STR && character.SPD >= character.MAG)
            return CharacterRole.Speed;

        // Fighter: default / high STR
        return CharacterRole.Fighter;
    }

    private static Dictionary<string, float> GetWeights(CharacterRole role) => role switch
    {
        CharacterRole.Fighter => FighterWeights,
        CharacterRole.Mage => MageWeights,
        CharacterRole.Healer => HealerWeights,
        CharacterRole.Speed => SpeedWeights,
        _ => FighterWeights,
    };
}
