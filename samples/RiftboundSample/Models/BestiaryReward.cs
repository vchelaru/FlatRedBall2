namespace RiftboundSample.Models;

public class BestiaryReward
{
    /// <summary>Number of unique enemies that must be recorded to unlock this reward.</summary>
    public int RequiredEntries { get; set; }

    /// <summary>"item", "gold", "recipe", or "ability".</summary>
    public string RewardType { get; set; } = "";

    public string RewardId { get; set; } = "";
    public int RewardCount { get; set; }
    public string Description { get; set; } = "";
}
