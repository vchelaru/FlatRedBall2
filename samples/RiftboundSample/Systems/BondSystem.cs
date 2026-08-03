using RiftboundSample.Models;

namespace RiftboundSample.Systems;

/// <summary>
/// Tracks character bond levels and triggers bond conversations at thresholds.
/// Bond thresholds: 20, 40, 60, 80, 100.
/// </summary>
public class BondSystem
{
    private static readonly int[] BondThresholds = [20, 40, 60, 80, 100];

    private readonly Dictionary<string, int> _bondLevels = [];
    private readonly Dictionary<string, HashSet<int>> _triggeredThresholds = [];
    private readonly DialogueSystem _dialogue;

    public BondSystem(DialogueSystem dialogue)
    {
        _dialogue = dialogue;
    }

    /// <summary>
    /// Returns the current bond level (0-100) for a character.
    /// </summary>
    public int GetBondLevel(string characterId)
    {
        return _bondLevels.GetValueOrDefault(characterId, 0);
    }

    /// <summary>
    /// Sets the bond level for a character and returns any newly crossed threshold dialogue IDs.
    /// </summary>
    public List<string> SetBondLevel(string characterId, int level, CharacterData characterData)
    {
        _bondLevels[characterId] = level;
        if (!_triggeredThresholds.ContainsKey(characterId))
            _triggeredThresholds[characterId] = [];

        var newDialogues = new List<string>();
        foreach (int threshold in BondThresholds)
        {
            if (level >= threshold
                && _triggeredThresholds[characterId].Add(threshold)
                && characterData.BondConversations.TryGetValue(threshold, out var dialogueId))
            {
                newDialogues.Add(dialogueId);
            }
        }
        return newDialogues;
    }

    /// <summary>
    /// Increases the bond level and checks for new conversation thresholds.
    /// Returns dialogue node IDs for any newly triggered bond conversations.
    /// </summary>
    public List<string> IncreaseBond(string characterId, int amount, CharacterData characterData)
    {
        int current = GetBondLevel(characterId);
        int newLevel = Math.Min(100, current + amount);
        return SetBondLevel(characterId, newLevel, characterData);
    }

    /// <summary>
    /// Starts a bond conversation using the DialogueSystem.
    /// </summary>
    public void StartConversation(string dialogueNodeId)
    {
        _dialogue.StartDialogue(dialogueNodeId);
    }

    /// <summary>
    /// Returns the next bond threshold that has not been reached for a character, or null if all reached.
    /// </summary>
    public int? GetNextThreshold(string characterId)
    {
        int level = GetBondLevel(characterId);
        foreach (int threshold in BondThresholds)
        {
            if (level < threshold)
                return threshold;
        }
        return null;
    }
}
