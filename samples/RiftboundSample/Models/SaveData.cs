namespace RiftboundSample.Models;

public class SaveData
{
    public PartyState Party { get; set; } = new();
    public string CurrentScreen { get; set; } = "";
    public float PlayerX { get; set; }
    public float PlayerY { get; set; }
    public string CurrentMap { get; set; } = "";
    public List<string> CompletedQuests { get; set; } = [];
    public List<string> DiscoveredRecipes { get; set; } = [];
    public Dictionary<string, bool> Flags { get; set; } = [];
    public List<string> VisitedMaps { get; set; } = [];
    public List<string> CompletedStoryEvents { get; set; } = [];
    public List<string> ShownTutorials { get; set; } = [];
    public DateTime SaveTime { get; set; }
    public TimeSpan PlayTime { get; set; }
}
