namespace RiftboundSample.Systems;

/// <summary>
/// Tracks which maps the player has visited, enabling fast travel to previously explored areas.
/// </summary>
public class FastTravelSystem
{
    public HashSet<string> VisitedMaps { get; private set; } = [];

    public void MarkVisited(string mapId) => VisitedMaps.Add(mapId);

    public bool HasVisited(string mapId) => VisitedMaps.Contains(mapId);

    /// <summary>
    /// Restores visited maps from save data.
    /// </summary>
    public void LoadFrom(IEnumerable<string> visitedMaps)
    {
        VisitedMaps = [..visitedMaps];
    }

    /// <summary>
    /// Returns visited maps sorted alphabetically for consistent display.
    /// </summary>
    public List<string> GetSortedVisitedMaps() => VisitedMaps.OrderBy(m => m).ToList();
}
