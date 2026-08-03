namespace RiftboundSample.Systems;

/// <summary>
/// Tracks first-time tutorials. Each tutorial ID is shown once, then recorded in flags.
/// </summary>
public class TutorialSystem
{
    private readonly HashSet<string> _shownTutorials;

    public static readonly string BattleBasics = "tutorial_battle_basics";
    public static readonly string PetCare = "tutorial_pet_care";
    public static readonly string Crafting = "tutorial_crafting";
    public static readonly string Equipment = "tutorial_equipment";

    public TutorialSystem(HashSet<string> flags)
    {
        _shownTutorials = flags;
    }

    /// <summary>
    /// Returns the tutorial text if the tutorial hasn't been shown yet, otherwise null.
    /// Marks the tutorial as shown upon returning text.
    /// </summary>
    public string? TryGetTutorial(string tutorialId)
    {
        if (_shownTutorials.Contains(tutorialId))
            return null;

        _shownTutorials.Add(tutorialId);
        return GetTutorialText(tutorialId);
    }

    public bool HasShown(string tutorialId) => _shownTutorials.Contains(tutorialId);

    private static string? GetTutorialText(string id) => id switch
    {
        "tutorial_battle_basics" =>
            "Battle Basics: Combat uses an Active Time Battle system. " +
            "Wait for a character's ATB gauge to fill, then choose an ability and target. " +
            "Press S to change speed, A for auto-battle, F to flee.",

        "tutorial_pet_care" =>
            "Pet Care: Your pets have Hunger, Happiness, and Energy stats that decay over time. " +
            "Feed, play with, and rest your pets to keep them happy. " +
            "Happy pets perform better in battle!",

        "tutorial_crafting" =>
            "Crafting: Collect materials from defeated enemies and the world. " +
            "Discover recipes by completing quests, then combine ingredients to create gear.",

        "tutorial_equipment" =>
            "Equipment: Visit shops to buy weapons and armor for your party. " +
            "Each character can equip a weapon and armor piece. " +
            "Better gear means better stats in battle.",

        _ => null,
    };
}
