using System.Text.Json;

namespace RiftboundSample.Systems;

public class StoryEvent
{
    public string Id { get; set; } = "";
    public string TriggerType { get; set; } = "";
    public string TriggerValue { get; set; } = "";
    public List<string> RequiredFlags { get; set; } = [];
    public List<string> SetFlags { get; set; } = [];
    public string DialogueFile { get; set; } = "";
    public string? StartBattle { get; set; }
    public string? RecruitCharacter { get; set; }
    public string? RecruitPet { get; set; }
    public string? UnlockMap { get; set; }
}

/// <summary>
/// Triggers scripted events based on game state (flags, quests, map entry, boss defeat).
/// Events fire once — completed events are tracked and never re-triggered.
/// </summary>
public class StoryEventSystem
{
    private readonly Dictionary<string, StoryEvent> _events = [];
    private readonly HashSet<string> _completedEvents = [];

    public IReadOnlySet<string> CompletedEvents => _completedEvents;

    public void LoadFromFile(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string json = File.ReadAllText(DataPath.Resolve(path));
        var events = JsonSerializer.Deserialize<List<StoryEvent>>(json, options) ?? [];
        foreach (var evt in events)
            _events[evt.Id] = evt;
    }

    /// <summary>
    /// Checks if any event matches the given trigger and current flags.
    /// Returns the first matching event, or null if none match.
    /// </summary>
    public StoryEvent? CheckTrigger(string triggerType, string triggerValue, HashSet<string> flags)
    {
        foreach (var evt in _events.Values)
        {
            if (_completedEvents.Contains(evt.Id))
                continue;

            if (evt.TriggerType != triggerType || evt.TriggerValue != triggerValue)
                continue;

            if (evt.RequiredFlags.Count > 0 && !evt.RequiredFlags.All(flags.Contains))
                continue;

            return evt;
        }

        return null;
    }

    public void CompleteEvent(string eventId)
    {
        _completedEvents.Add(eventId);
    }

    /// <summary>Restores completed events from save data.</summary>
    public void RestoreCompleted(IEnumerable<string> completedEventIds)
    {
        foreach (var id in completedEventIds)
            _completedEvents.Add(id);
    }
}
