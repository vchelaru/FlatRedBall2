using RiftboundSample.Models;

namespace RiftboundSample.Systems;

/// <summary>
/// Manages New Game+ state. After completing the game, players can start over
/// with characters at current levels, all recipes discovered, and bestiary entries preserved.
/// Enemies scale up by 50%.
/// </summary>
public class NewGamePlusSystem
{
    /// <summary>
    /// Creates NG+ save data from a completed game save.
    /// Characters keep their levels, recipes stay discovered, bestiary is preserved.
    /// </summary>
    public static SaveData CreateNewGamePlusSave(SaveData completedSave)
    {
        return new SaveData
        {
            Party = completedSave.Party,
            CurrentScreen = nameof(Screens.OverworldScreen),
            PlayerX = 0,
            PlayerY = 0,
            CurrentMap = "brasshollow",
            CompletedQuests = [],
            DiscoveredRecipes = new List<string>(completedSave.DiscoveredRecipes),
            Flags = new Dictionary<string, bool>(completedSave.Flags)
            {
                ["new_game_plus"] = true,
                ["ng_plus_unlocked"] = true,
            },
            SaveTime = completedSave.SaveTime,
            PlayTime = completedSave.PlayTime,
        };
    }

    /// <summary>
    /// Returns whether the save is a New Game+ save.
    /// </summary>
    public static bool IsNewGamePlus(SaveData data)
        => data.Flags.TryGetValue("new_game_plus", out bool v) && v;

    /// <summary>
    /// Enemy stat scaling for NG+: all base stats multiplied by 1.5.
    /// </summary>
    public static EnemyData ScaleEnemy(EnemyData original)
    {
        return new EnemyData
        {
            Id = original.Id,
            Name = original.Name,
            HP = (int)(original.HP * 1.5f),
            MP = (int)(original.MP * 1.5f),
            STR = (int)(original.STR * 1.5f),
            MAG = (int)(original.MAG * 1.5f),
            DEF = (int)(original.DEF * 1.5f),
            RES = (int)(original.RES * 1.5f),
            SPD = (int)(original.SPD * 1.5f),
            IsBoss = original.IsBoss,
            XPReward = (int)(original.XPReward * 1.5f),
            AbilityIds = new List<string>(original.AbilityIds),
            ElementAffinities = original.ElementAffinities.Select(a => new ElementAffinity
            {
                Element = a.Element,
                Multiplier = a.Multiplier,
            }).ToList(),
            DropTable = original.DropTable.Select(d => new DropEntry
            {
                ItemId = d.ItemId,
                Rate = d.Rate,
            }).ToList(),
        };
    }
}
