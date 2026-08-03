using RiftboundSample.Models;
using RiftboundSample.Systems;
using Shouldly;
using Xunit;

namespace RiftboundSample.Tests.Systems;

public class ATBSystemTests
{
    [Fact]
    public void GetReadyCombatants_AfterSufficientTime_ReturnsReady()
    {
        var spd = 100; // fill rate = 1.0/sec, ready after 1 second
        var combatant = new CombatantState { Id = "hero", SPD = spd, CurrentHP = 10, MaxHP = 10 };
        var atb = new ATBSystem();
        atb.AddCombatant(combatant);

        atb.Update(1.0f);

        atb.GetReadyCombatants().Count.ShouldBe(1);
        atb.GetReadyCombatants()[0].Id.ShouldBe("hero");
    }

    [Fact]
    public void GetReadyCombatants_NotEnoughTime_ReturnsEmpty()
    {
        var spd = 50; // fill rate = 0.5/sec, needs 2 seconds
        var combatant = new CombatantState { Id = "hero", SPD = spd, CurrentHP = 10, MaxHP = 10 };
        var atb = new ATBSystem();
        atb.AddCombatant(combatant);

        atb.Update(0.5f);

        atb.GetReadyCombatants().ShouldBeEmpty();
    }

    [Fact]
    public void ResetGauge_AfterAction_GaugeIsZero()
    {
        var combatant = new CombatantState { Id = "hero", SPD = 100, CurrentHP = 10, MaxHP = 10 };
        var atb = new ATBSystem();
        atb.AddCombatant(combatant);
        atb.Update(2.0f);

        atb.ResetGauge("hero");

        combatant.ATB.ShouldBe(0f);
        atb.GetReadyCombatants().ShouldBeEmpty();
    }

    [Fact]
    public void SetPaused_WhenPaused_DoesNotAdvanceGauges()
    {
        var combatant = new CombatantState { Id = "hero", SPD = 100, CurrentHP = 10, MaxHP = 10 };
        var atb = new ATBSystem();
        atb.AddCombatant(combatant);

        atb.SetPaused(true);
        atb.Update(5.0f);

        combatant.ATB.ShouldBe(0f);
    }

    [Fact]
    public void Update_SpeedMultiplier_AffectsFillRate()
    {
        var spd = 50; // base fill = 0.5/sec, with 2x = 1.0/sec
        var combatant = new CombatantState { Id = "hero", SPD = spd, CurrentHP = 10, MaxHP = 10 };
        var atb = new ATBSystem { SpeedMultiplier = 2f };
        atb.AddCombatant(combatant);

        atb.Update(1.0f);

        atb.GetReadyCombatants().Count.ShouldBe(1);
    }

    [Fact]
    public void ApplySurpriseDelay_SetsNegativeGauge_StillFillsNormally()
    {
        float spd = 100; // fill rate = 1.0/sec
        float delaySeconds = 0.5f;
        var combatant = new CombatantState { Id = "hero", SPD = (int)spd, CurrentHP = 10, MaxHP = 10 };
        var atb = new ATBSystem();
        atb.AddCombatant(combatant);

        atb.ApplySurpriseDelay("hero", delaySeconds);

        // Gauge should be negative
        combatant.ATB.ShouldBeLessThan(0f);

        // After 0.5s: gauge goes from -0.5 to 0.0 (not ready)
        atb.Update(0.5f);
        atb.GetReadyCombatants().ShouldBeEmpty();

        // After another 1.0s: gauge goes from 0.0 to 1.0 (ready)
        atb.Update(1.0f);
        atb.GetReadyCombatants().Count.ShouldBe(1);
    }

    [Fact]
    public void Update_DeadCombatant_DoesNotFill()
    {
        var combatant = new CombatantState { Id = "dead", SPD = 100, CurrentHP = 0, MaxHP = 10 };
        var atb = new ATBSystem();
        atb.AddCombatant(combatant);

        atb.Update(5.0f);

        combatant.ATB.ShouldBe(0f);
    }
}
