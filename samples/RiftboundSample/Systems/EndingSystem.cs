using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public enum EndingType
{
    True,
    Good,
    Bittersweet
}

/// <summary>
/// Determines which ending the player receives based on party state and quest completion.
/// </summary>
public class EndingSystem
{
    /// <summary>Total recruitable characters (excluding Kael who is always in the party).</summary>
    private const int TotalRecruitableCharacters = 21;

    private const int RequiredBondForTrueEnding = 80;

    private static readonly string[] MainQuestIds =
    [
        "the_kernel_quest",
        "nightmares_end",
        "draven_must_fall",
    ];

    /// <summary>
    /// Determines the ending based on party composition, bond levels, and quest completion.
    /// </summary>
    /// <param name="party">Current party state with roster and bond info.</param>
    /// <param name="completedQuests">List of completed quest IDs.</param>
    /// <param name="bondLevels">Character ID to bond level mapping.</param>
    public EndingType DetermineEnding(
        PartyState party,
        List<string> completedQuests,
        Dictionary<string, int> bondLevels)
    {
        bool liraRecruited = party.Roster.Contains("lira");
        bool allMainQuestsComplete = MainQuestIds.All(completedQuests.Contains);
        bool allRecruited = party.Roster.Count >= TotalRecruitableCharacters;

        int liraBond = bondLevels.GetValueOrDefault("lira", 0);
        bool liraHighBond = liraBond >= RequiredBondForTrueEnding;

        // True Ending: everyone recruited, Lira bond high, all main quests done
        if (allRecruited && liraHighBond && allMainQuestsComplete)
            return EndingType.True;

        // Good Ending: Lira rescued and main quests complete
        if (liraRecruited && allMainQuestsComplete)
            return EndingType.Good;

        // Bittersweet: Lira rescued but party incomplete or quests missed
        return EndingType.Bittersweet;
    }

    public static string GetEndingTitle(EndingType ending) => ending switch
    {
        EndingType.True => "The Rift Healed",
        EndingType.Good => "A New Dawn",
        EndingType.Bittersweet => "The Price of Victory",
        _ => "The End",
    };

    public static string[] GetEndingNarration(EndingType ending) => ending switch
    {
        EndingType.True =>
        [
            "With Chancellor Draven defeated and the rift sealed, all three worlds begin to heal.",
            "Lira stands beside Kael, her power now a bridge between realms rather than a weapon.",
            "The Architect rebuilds what was broken, and every companion who fought by your side finds peace.",
            "The bonds forged in battle become the foundation of a new era — one where steam, dreams, and code coexist in harmony.",
            "This is not just an ending. It is a beginning.",
        ],
        EndingType.Good =>
        [
            "Chancellor Draven falls, and the rift begins to close.",
            "Lira is safe, reunited with Kael at last. The worlds begin to separate, but gently this time.",
            "Not all wounds are healed. Some companions were lost along the way, and some corners of the realm remain scarred.",
            "But there is hope. The dawn breaks warm and golden over Brasshollow.",
            "The journey is over. A new chapter awaits.",
        ],
        EndingType.Bittersweet =>
        [
            "Draven is gone, but the cost was steep.",
            "Lira survived, yet the rift's closure tore away parts of the world that can never be restored.",
            "Friends were lost. Promises were broken. The victory feels hollow in the quiet aftermath.",
            "Kael holds Lira close and looks out over what remains.",
            "It will have to be enough.",
        ],
        _ => ["The End."],
    };
}
