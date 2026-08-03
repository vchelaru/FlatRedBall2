using RiftboundSample.Models;

namespace RiftboundSample.Systems;

/// <summary>
/// Cross-system event bus for decoupling game systems.
/// Wire up: battle victory -> EnemyDefeated -> bestiary + quest progress.
/// </summary>
public class GameEvents
{
    public event Action<string>? EnemyDefeated;
    public event Action<string, int>? ItemCollected;
    public event Action<string>? QuestCompleted;
    public event Action<string>? RecipeDiscovered;
    public event Action<PetState>? PetDied;
    public event Action<string, int>? BondLevelUp;

    public void OnEnemyDefeated(string id) => EnemyDefeated?.Invoke(id);
    public void OnItemCollected(string itemId, int count) => ItemCollected?.Invoke(itemId, count);
    public void OnQuestCompleted(string questId) => QuestCompleted?.Invoke(questId);
    public void OnRecipeDiscovered(string recipeId) => RecipeDiscovered?.Invoke(recipeId);
    public void OnPetDied(PetState pet) => PetDied?.Invoke(pet);
    public void OnBondLevelUp(string characterId, int newLevel) => BondLevelUp?.Invoke(characterId, newLevel);
}
