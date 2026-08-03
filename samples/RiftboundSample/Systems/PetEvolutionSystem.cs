using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class PetEvolutionSystem
{
    private readonly Dictionary<string, PetEvolution> _evolutionLookup;

    public PetEvolutionSystem(List<PetEvolution> evolutions)
    {
        _evolutionLookup = evolutions.ToDictionary(e => e.PetId);
    }

    public bool CanEvolve(PetState pet) => pet.Bond >= 100 && !pet.IsEvolved;

    public PetEvolution? GetEvolution(string petId) =>
        _evolutionLookup.TryGetValue(petId, out var evo) ? evo : null;

    /// <summary>
    /// Evolves a pet, updating its name and setting the evolved flag.
    /// Returns the updated pet state, or null if evolution conditions aren't met.
    /// </summary>
    public PetState? Evolve(PetState pet)
    {
        if (!CanEvolve(pet)) return null;
        if (!_evolutionLookup.TryGetValue(pet.Id, out var evolution)) return null;

        pet.IsEvolved = true;
        pet.EvolvedName = evolution.EvolvedName;
        return pet;
    }
}
