using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class QuestSystem
{
    private readonly Dictionary<string, QuestData> _questCatalog = [];
    private readonly Dictionary<string, QuestData> _active = [];
    private readonly HashSet<string> _completed = [];
    private readonly PartyState _party;
    private readonly GameEvents _events;

    public QuestSystem(List<QuestData> quests, PartyState party, GameEvents events)
    {
        foreach (var q in quests)
            _questCatalog[q.Id] = q;
        _party = party;
        _events = events;

        // Wire up event bus for automatic objective tracking
        events.EnemyDefeated += id => UpdateObjective("defeat", id, 1);
        events.ItemCollected += (id, count) => UpdateObjective("collect", id, count);
    }

    public bool AcceptQuest(string questId)
    {
        if (_active.ContainsKey(questId) || _completed.Contains(questId))
            return false;
        if (!_questCatalog.TryGetValue(questId, out var quest))
            return false;

        // Deep copy so each active quest has independent objective state
        var copy = new QuestData
        {
            Id = quest.Id,
            Name = quest.Name,
            Description = quest.Description,
            Giver = quest.Giver,
            IsMainQuest = quest.IsMainQuest,
            Reward = quest.Reward,
            Objectives = quest.Objectives.Select(o => new QuestObjective
            {
                Type = o.Type,
                TargetId = o.TargetId,
                RequiredCount = o.RequiredCount,
                CurrentCount = 0,
            }).ToList(),
        };
        _active[questId] = copy;
        return true;
    }

    /// <summary>
    /// Updates progress on all active quests matching the given type and target.
    /// </summary>
    public void UpdateObjective(string type, string targetId, int count)
    {
        foreach (var quest in _active.Values)
        {
            foreach (var obj in quest.Objectives)
            {
                if (obj.Type == type && obj.TargetId == targetId)
                    obj.CurrentCount = Math.Min(obj.CurrentCount + count, obj.RequiredCount);
            }
        }
    }

    /// <summary>
    /// Returns true if all objectives for the quest are complete.
    /// </summary>
    public bool IsQuestComplete(string questId)
    {
        if (!_active.TryGetValue(questId, out var quest)) return false;
        return quest.Objectives.All(o => o.CurrentCount >= o.RequiredCount);
    }

    /// <summary>
    /// Completes a quest: grants rewards and marks it done. Returns false if not completable.
    /// </summary>
    public bool CompleteQuest(string questId)
    {
        if (!IsQuestComplete(questId)) return false;
        if (!_active.TryGetValue(questId, out var quest)) return false;

        var reward = quest.Reward;

        // Grant gold
        if (reward.Gold > 0)
        {
            if (_party.Inventory.ContainsKey("gold"))
                _party.Inventory["gold"] += reward.Gold;
            else
                _party.Inventory["gold"] = reward.Gold;
        }

        // Grant items
        foreach (var (itemId, count) in reward.Items)
        {
            if (_party.Inventory.ContainsKey(itemId))
                _party.Inventory[itemId] += count;
            else
                _party.Inventory[itemId] = count;
        }

        // Unlock recipe
        if (reward.UnlockRecipeId != null)
            _events.OnRecipeDiscovered(reward.UnlockRecipeId);

        _active.Remove(questId);
        _completed.Add(questId);
        _events.OnQuestCompleted(questId);
        return true;
    }

    public List<QuestData> GetActiveQuests() => _active.Values.ToList();
    public List<string> GetCompletedQuestIds() => _completed.ToList();
    public bool IsActive(string questId) => _active.ContainsKey(questId);
    public bool IsCompleted(string questId) => _completed.Contains(questId);
}
