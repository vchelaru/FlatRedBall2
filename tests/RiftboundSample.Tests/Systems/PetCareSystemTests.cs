using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class PetCareSystemTests
{
    private static PetData MakePetData() => new()
    {
        Id = "cogsworth",
        Name = "Cogsworth",
        OwnerCharacterId = "kael",
        SatietyDecayRate = 0.5f,   // per minute
        TrainingDecayRate = 0.25f,
        BondDecayRate = 0.1f,
    };

    private static PetState MakePetState(float satiety = 80, float training = 50, float bond = 50) => new()
    {
        Id = "cogsworth",
        Name = "Cogsworth",
        OwnerId = "kael",
        Satiety = satiety,
        Training = training,
        Bond = bond,
    };

    private static PetCareSystem MakeSystem(PetData? data = null)
    {
        var d = data ?? MakePetData();
        return new PetCareSystem(new Dictionary<string, PetData> { [d.Id] = d }, new Random(42));
    }

    [Fact]
    public void CheckDeath_BothStatsZero_PetDies()
    {
        var system = MakeSystem();
        var pet = MakePetState(satiety: 0, training: 0, bond: 10);

        var grief = system.CheckDeath(pet);

        pet.IsAlive.ShouldBeFalse();
        grief.ShouldNotBeNull();
        grief.Name.ShouldBe("Grief");
        grief.StatMultiplier.ShouldBe(0.85f);
        grief.RemainingTurns.ShouldBe(-1);
    }

    [Fact]
    public void CheckDeath_SatietyAboveZero_PetSurvives()
    {
        var system = MakeSystem();
        var pet = MakePetState(satiety: 1, training: 0, bond: 10);

        var grief = system.CheckDeath(pet);

        pet.IsAlive.ShouldBeTrue();
        grief.ShouldBeNull();
    }

    [Fact]
    public void Feed_BasicFood_RestoresSatiety()
    {
        var system = MakeSystem();
        var pet = MakePetState(satiety: 50);

        system.Feed(pet, "basic_food");

        pet.Satiety.ShouldBe(80f);
    }

    [Fact]
    public void Feed_PremiumFood_RestoresMoreSatiety()
    {
        var system = MakeSystem();
        var pet = MakePetState(satiety: 50);

        system.Feed(pet, "premium_food");

        pet.Satiety.ShouldBe(100f); // 50 + 60 capped at 100
    }

    [Fact]
    public void Train_IncreasesTraining()
    {
        var system = MakeSystem();
        var pet = MakePetState(training: 30);

        system.Train(pet);

        pet.Training.ShouldBeGreaterThanOrEqualTo(45f); // 30 + 15 minimum
        pet.Training.ShouldBeLessThanOrEqualTo(55f);    // 30 + 25 maximum
    }

    [Fact]
    public void Update_DecaysSatietyAndTraining()
    {
        var system = MakeSystem();
        var pet = MakePetState(satiety: 80, training: 50, bond: 50);
        float deltaSeconds = 60f; // one minute

        system.Update(deltaSeconds, [pet]);

        // SatietyDecayRate = 0.5/min, so after 1 min: 80 - 0.5 = 79.5
        pet.Satiety.ShouldBe(79.5f, tolerance: 0.01f);
        // TrainingDecayRate = 0.25/min, so after 1 min: 50 - 0.25 = 49.75
        pet.Training.ShouldBe(49.75f, tolerance: 0.01f);
        // Bond should NOT decay (Satiety > 20)
        pet.Bond.ShouldBe(50f);
    }

    [Fact]
    public void Update_BondDecaysWhenNeglected()
    {
        var system = MakeSystem();
        float satiety = 10f; // below 20 threshold
        float bond = 50f;
        var pet = MakePetState(satiety: satiety, training: 50, bond: bond);
        float deltaSeconds = 60f;

        system.Update(deltaSeconds, [pet]);

        // Bond should decay: BondDecayRate = 0.1/min
        pet.Bond.ShouldBe(49.9f, tolerance: 0.01f);
    }
}
