using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public static class ProgressionSystem
{
    /// <summary>
    /// XP required to reach a given level: 100 * level * (level + 1) / 2.
    /// Level 2 = 300, level 10 = 5500, level 50 = 127500.
    /// </summary>
    public static int XPForLevel(int level)
    {
        if (level <= 1) return 0;
        return 100 * level * (level + 1) / 2;
    }

    /// <summary>
    /// XP required from current level to next level.
    /// </summary>
    public static int XPToNextLevel(int currentLevel)
        => XPForLevel(currentLevel + 1) - XPForLevel(currentLevel);

    /// <summary>
    /// Applies a level-up to a character, adding growth rates for each level gained.
    /// Returns the updated CharacterData with new stats and level.
    /// </summary>
    public static CharacterData ApplyLevelUp(CharacterData data, int newLevel)
    {
        if (newLevel <= data.Level || data.Growth == null)
            return data;

        int levelsGained = newLevel - data.Level;
        data.HP += data.Growth.HP * levelsGained;
        data.MP += data.Growth.MP * levelsGained;
        data.STR += data.Growth.STR * levelsGained;
        data.MAG += data.Growth.MAG * levelsGained;
        data.DEF += data.Growth.DEF * levelsGained;
        data.RES += data.Growth.RES * levelsGained;
        data.SPD += data.Growth.SPD * levelsGained;
        data.LCK += data.Growth.LCK * levelsGained;
        data.Level = newLevel;
        data.XPToNextLevel = XPToNextLevel(newLevel);

        return data;
    }

    /// <summary>
    /// Adds XP to a character and applies level-ups as needed.
    /// Returns the number of levels gained.
    /// </summary>
    public static int AddXP(CharacterData data, int xpAmount)
    {
        int startLevel = data.Level;
        data.XP += xpAmount;

        while (data.XP >= data.XPToNextLevel && data.Level < 99)
        {
            data.XP -= data.XPToNextLevel;
            ApplyLevelUp(data, data.Level + 1);
        }

        return data.Level - startLevel;
    }

    /// <summary>
    /// Suggested level range per area.
    /// </summary>
    public static (int Min, int Max) AreaLevelRange(string areaId) => areaId switch
    {
        "brasshollow" => (1, 5),
        "rustfields" => (4, 8),
        "cogspire" => (7, 12),
        "fort_ironmaw" => (10, 15),
        "scorched_vents" => (13, 18),
        "ethereal_glade" or "dream_hollow" or "crystal_caverns" => (16, 30),
        "nexus_core" or "data_streams" or "memory_banks" => (28, 42),
        "the_fade" => (40, 50),
        _ => (1, 50),
    };
}
