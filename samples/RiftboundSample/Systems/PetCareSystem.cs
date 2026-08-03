using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class PetCareSystem
{
    private readonly Dictionary<string, PetData> _petDataLookup;
    private readonly Random _random;

    public PetCareSystem(Dictionary<string, PetData> petDataLookup, Random? random = null)
    {
        _petDataLookup = petDataLookup;
        _random = random ?? Random.Shared;
    }

    /// <summary>
    /// Decays pet stats over time. Decay rates are per-minute values from PetData,
    /// converted to per-second internally.
    /// </summary>
    public void Update(float deltaSeconds, List<PetState> pets)
    {
        foreach (var pet in pets)
        {
            if (!pet.IsAlive)
                continue;

            if (!_petDataLookup.TryGetValue(pet.Id, out var data))
                continue;

            // Decay rates in data are per-minute; convert to per-second
            float satietyDecayPerSec = data.SatietyDecayRate / 60f;
            float trainingDecayPerSec = data.TrainingDecayRate / 60f;

            pet.Satiety = Math.Max(0, pet.Satiety - satietyDecayPerSec * deltaSeconds);
            pet.Training = Math.Max(0, pet.Training - trainingDecayPerSec * deltaSeconds);

            // Bond only decays when neglected (Satiety < 20)
            if (pet.Satiety < 20)
            {
                float bondDecayPerSec = data.BondDecayRate / 60f;
                pet.Bond = Math.Max(0, pet.Bond - bondDecayPerSec * deltaSeconds);
            }
        }
    }

    /// <summary>
    /// Feed a pet. Basic food restores 30 Satiety, premium restores 60.
    /// </summary>
    public void Feed(PetState pet, string foodItemId)
    {
        if (!pet.IsAlive) return;

        float amount = foodItemId == "premium_food" ? 60f : 30f;
        pet.Satiety = Math.Min(100, pet.Satiety + amount);
    }

    /// <summary>
    /// Train a pet. Increases Training by 15-25 (random).
    /// </summary>
    public void Train(PetState pet)
    {
        if (!pet.IsAlive) return;

        float amount = 15 + _random.Next(11); // 15-25
        pet.Training = Math.Min(100, pet.Training + amount);
    }

    public void IncreaseBond(PetState pet, float amount)
    {
        if (!pet.IsAlive) return;
        pet.Bond = Math.Min(100, pet.Bond + amount);
    }

    /// <summary>
    /// Pet dies if both Satiety and Training reach 0.
    /// Returns the Grief StatusEffect to apply to the owner, or null if the pet is still alive.
    /// </summary>
    public StatusEffect? CheckDeath(PetState pet)
    {
        if (!pet.IsAlive) return null;
        if (pet.Satiety > 0 || pet.Training > 0) return null;

        pet.IsAlive = false;
        return new StatusEffect
        {
            Name = "Grief",
            StatMultiplier = 0.85f,
            RemainingTurns = -1,
        };
    }
}
