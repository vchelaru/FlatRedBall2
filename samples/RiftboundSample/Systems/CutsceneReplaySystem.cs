namespace RiftboundSample.Systems;

/// <summary>
/// Tracks which story events have been seen, allowing replay of their dialogue.
/// </summary>
public class CutsceneReplaySystem
{
    private readonly List<(string EventId, string DisplayName)> _seenEvents = [];
    private readonly HashSet<string> _seenIds = [];

    public IReadOnlyList<(string EventId, string DisplayName)> SeenEvents => _seenEvents;

    public void RecordEvent(string eventId, string displayName)
    {
        if (_seenIds.Add(eventId))
            _seenEvents.Add((eventId, displayName));
    }

    public bool HasSeen(string eventId) => _seenIds.Contains(eventId);

    /// <summary>Restores seen events from save data.</summary>
    public void Restore(IEnumerable<(string EventId, string DisplayName)> events)
    {
        foreach (var (id, name) in events)
        {
            if (_seenIds.Add(id))
                _seenEvents.Add((id, name));
        }
    }

    /// <summary>Map event IDs to display names for the replay list.</summary>
    public static string GetDisplayName(string eventId) => eventId switch
    {
        "act1_intro" => "Ch.1 — The Rift Opens",
        "act1_meet_mira" => "Ch.1 — Meeting Mira",
        "act1_elder" => "Ch.2 — The Elder's Counsel",
        "act1_venn" => "Ch.2 — Professor Venn",
        "act1_thorne" => "Ch.3 — Captain Thorne",
        "act1_pip" => "Ch.3 — The Little Thief",
        "act1_sera" => "Ch.3 — Sera the Healer",
        "act1_forge" => "Ch.4 — The Blacksmith Forge",
        "act1_boss_furnace" => "Ch.4 — The Furnace Guardian",
        "act1_rift_opens" => "Ch.4 — Into the Ethereal",
        "act2_enter_ethereal" => "Ch.5 — Crystal Glade",
        "act2_lyris" => "Ch.5 — Lyris of the Glade",
        "act2_solace" => "Ch.6 — Solace at the Temple",
        "act2_wraith" => "Ch.6 — The Wraith",
        "act2_orin" => "Ch.7 — Orin the Scholar",
        "act2_zephyr" => "Ch.7 — Zephyr's Wind",
        "act2_nyx" => "Ch.7 — The Mysterious Nyx",
        "act2_echo" => "Ch.8 — Echo's Memory",
        "act2_nightmare_boss" => "Ch.8 — The Nightmare Core",
        "act2_dreamer_boss" => "Ch.8 — The Dreamer",
        "act2_nexus_revealed" => "Ch.8 — The Nexus Revealed",
        "act3_enter_nexus" => "Ch.9 — Into the Data Core",
        "act3_byte" => "Ch.9 — Byte",
        "act3_pixel" => "Ch.9 — Pixel",
        "act3_cipher" => "Ch.10 — Cipher",
        "act3_glitch" => "Ch.10 — Glitch",
        "act3_nova" => "Ch.10 — Nova",
        "act3_root" => "Ch.11 — Root",
        "act3_kernel_boss" => "Ch.11 — The Kernel",
        "act3_system_override" => "Ch.11 — System Override",
        "act3_fade_opens" => "Ch.11 — The Fade Opens",
        "act4_enter_fade" => "Ch.12 — Into the Fade",
        "act4_lira_found" => "Ch.13 — Finding Lira",
        "act4_architect" => "Ch.13 — The Architect",
        "act4_draven_confrontation" => "Ch.14 — Draven's Last Stand",
        "act4_final_boss" => "Ch.14 — The Final Battle",
        "act4_ending" => "Ch.14 — The End",
        "dream1" => "Dream — Alone in the Ethereal",
        "dream2" => "Dream — Getting Closer",
        "dream3" => "Dream — I'll Hold On",
        _ => eventId,
    };
}
