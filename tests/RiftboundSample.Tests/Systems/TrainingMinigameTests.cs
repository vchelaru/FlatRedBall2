using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class TrainingMinigameTests
{
    [Fact]
    public void Start_Timing_SetsPhaseToActive()
    {
        var game = new TrainingMinigame(new Random(42));
        game.Start(MinigameType.Timing);

        game.Phase.ShouldBe(MinigamePhase.Active);
        game.Type.ShouldBe(MinigameType.Timing);
    }

    [Fact]
    public void Update_ExceedsMaxDuration_Completes()
    {
        var game = new TrainingMinigame(new Random(42));
        game.Start(MinigameType.Reaction);

        // Advance past 30s max duration
        game.Update(31f);

        game.Phase.ShouldBe(MinigamePhase.Complete);
    }

    [Fact]
    public void ApplyReward_AfterCompletion_IncreasesTraining()
    {
        var game = new TrainingMinigame(new Random(42));
        game.Start(MinigameType.Timing);
        // Force completion by exceeding time
        game.Update(31f);

        float startTraining = 50f;
        var pet = new PetState { Training = startTraining };
        game.ApplyReward(pet);

        // Should increase training by at least MinReward (15)
        pet.Training.ShouldBeGreaterThan(startTraining);
    }

    [Fact]
    public void TrainingReward_RangeIsCorrect()
    {
        var game = new TrainingMinigame(new Random(42));
        game.Start(MinigameType.Timing);
        game.Update(31f);

        float minReward = 15f;
        float maxReward = 25f;
        game.TrainingReward.ShouldBeGreaterThanOrEqualTo(minReward);
        game.TrainingReward.ShouldBeLessThanOrEqualTo(maxReward);
    }
}
