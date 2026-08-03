using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class PetEvolutionSystemTests
{
    private static PetEvolutionSystem MakeSystem() => new(
    [
        new PetEvolution
        {
            PetId = "cogsworth",
            EvolvedName = "Cogsworth Prime",
            EvolvedAbilityBasic = "cog_barrage",
            EvolvedAbilityAdvanced = "gear_tempest",
            EvolvedAbilityUltimate = "overdrive_max",
            StatBoost = 1.5f,
        }
    ]);

    [Fact]
    public void CanEvolve_BondAt100AndNotEvolved_ReturnsTrue()
    {
        var system = MakeSystem();
        var pet = new PetState { Id = "cogsworth", Bond = 100, IsEvolved = false };

        system.CanEvolve(pet).ShouldBeTrue();
    }

    [Fact]
    public void CanEvolve_BondBelow100_ReturnsFalse()
    {
        var system = MakeSystem();
        var pet = new PetState { Id = "cogsworth", Bond = 99, IsEvolved = false };

        system.CanEvolve(pet).ShouldBeFalse();
    }

    [Fact]
    public void CanEvolve_AlreadyEvolved_ReturnsFalse()
    {
        var system = MakeSystem();
        var pet = new PetState { Id = "cogsworth", Bond = 100, IsEvolved = true };

        system.CanEvolve(pet).ShouldBeFalse();
    }

    [Fact]
    public void Evolve_EligiblePet_SetsEvolvedState()
    {
        var system = MakeSystem();
        string expectedName = "Cogsworth Prime";
        var pet = new PetState { Id = "cogsworth", Name = "Cogsworth", Bond = 100, IsEvolved = false };

        var result = system.Evolve(pet);

        result.ShouldNotBeNull();
        result.IsEvolved.ShouldBeTrue();
        result.EvolvedName.ShouldBe(expectedName);
    }

    [Fact]
    public void Evolve_IneligiblePet_ReturnsNull()
    {
        var system = MakeSystem();
        var pet = new PetState { Id = "cogsworth", Bond = 50, IsEvolved = false };

        system.Evolve(pet).ShouldBeNull();
    }
}
