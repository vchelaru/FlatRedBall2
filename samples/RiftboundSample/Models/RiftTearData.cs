namespace RiftboundSample.Models;

public class RiftTearReward
{
    public string Type { get; set; } = "";  // "item" or "recipe"
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
}

public class RiftTearData
{
    public string Id { get; set; } = "";
    public string Location { get; set; } = "";
    public int Difficulty { get; set; }
    public RiftTearReward Reward { get; set; } = new();
}
