namespace RiftboundSample.Models;

public class QuestData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Giver { get; set; } = "";
    public List<QuestObjective> Objectives { get; set; } = [];
    public QuestReward Reward { get; set; } = new();
    public bool IsMainQuest { get; set; }
}

public class QuestObjective
{
    public string Type { get; set; } = "";
    public string TargetId { get; set; } = "";
    public int RequiredCount { get; set; }
    public int CurrentCount { get; set; }
}

public class QuestReward
{
    public int Gold { get; set; }
    public int XP { get; set; }
    public Dictionary<string, int> Items { get; set; } = [];
    public string? UnlockRecipeId { get; set; }
}
