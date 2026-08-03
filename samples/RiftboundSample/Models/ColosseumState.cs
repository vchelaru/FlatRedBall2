namespace RiftboundSample.Models;

public class ColosseumState
{
    public int CurrentWave { get; set; }
    public int TotalXPEarned { get; set; }
    public int TotalGoldEarned { get; set; }
    public List<(string ItemId, int Count)> ItemsEarned { get; set; } = [];
    public int HighestWave { get; set; }
}
