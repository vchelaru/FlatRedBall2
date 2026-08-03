using RiftboundSample.Models;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Models;

public class PetStateTests
{
    [Theory]
    [InlineData(0, PetTier.Basic)]
    [InlineData(39, PetTier.Basic)]
    [InlineData(40, PetTier.Advanced)]
    [InlineData(79, PetTier.Advanced)]
    [InlineData(80, PetTier.Ultimate)]
    [InlineData(100, PetTier.Ultimate)]
    public void CurrentTier_BondThresholds_ReturnsCorrectTier(float bond, PetTier expectedTier)
    {
        var pet = new PetState { Bond = bond };
        pet.CurrentTier.ShouldBe(expectedTier);
    }

    [Fact]
    public void GetEffectiveStatMultiplier_GriefDebuff_Reduces()
    {
        float griefMultiplier = 0.85f;
        var combatant = new CombatantState
        {
            Id = "hero", STR = 20,
            StatusEffects = [new StatusEffect { Name = "Grief", StatMultiplier = griefMultiplier }]
        };

        combatant.GetEffectiveStatMultiplier().ShouldBe(griefMultiplier);
    }

    [Fact]
    public void GetEffectiveStatMultiplier_NoEffects_ReturnsOne()
    {
        var combatant = new CombatantState { Id = "hero", STR = 20 };
        combatant.GetEffectiveStatMultiplier().ShouldBe(1f);
    }
}
