using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class CraftingSystem
{
    /// <summary>
    /// Returns true if the party has enough materials for one craft of this recipe.
    /// </summary>
    public bool CanCraft(RecipeData recipe, PartyState party)
    {
        foreach (var (materialId, needed) in recipe.Materials)
        {
            if (!party.Inventory.TryGetValue(materialId, out int have) || have < needed)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Crafts the recipe the specified number of times. Deducts materials and adds output.
    /// Returns the actual number crafted (clamped to what materials allow).
    /// </summary>
    public int Craft(RecipeData recipe, PartyState party, int count = 1)
    {
        int maxCraftable = GetMaxCraftable(recipe, party);
        int actual = Math.Min(count, maxCraftable);
        if (actual <= 0) return 0;

        // Deduct materials
        foreach (var (materialId, needed) in recipe.Materials)
        {
            party.Inventory[materialId] -= needed * actual;
            if (party.Inventory[materialId] <= 0)
                party.Inventory.Remove(materialId);
        }

        // Add output
        int outputTotal = recipe.OutputCount * actual;
        if (party.Inventory.ContainsKey(recipe.OutputItemId))
            party.Inventory[recipe.OutputItemId] += outputTotal;
        else
            party.Inventory[recipe.OutputItemId] = outputTotal;

        return actual;
    }

    /// <summary>
    /// Returns all recipes the player currently has materials to craft at least once.
    /// </summary>
    public List<RecipeData> GetCraftableRecipes(List<RecipeData> all, PartyState party)
    {
        return all.Where(r => CanCraft(r, party)).ToList();
    }

    /// <summary>
    /// Returns the maximum number of times a recipe can be crafted with current materials.
    /// </summary>
    public int GetMaxCraftable(RecipeData recipe, PartyState party)
    {
        int max = int.MaxValue;
        foreach (var (materialId, needed) in recipe.Materials)
        {
            if (!party.Inventory.TryGetValue(materialId, out int have))
                return 0;
            max = Math.Min(max, have / needed);
        }
        return max == int.MaxValue ? 0 : max;
    }
}
