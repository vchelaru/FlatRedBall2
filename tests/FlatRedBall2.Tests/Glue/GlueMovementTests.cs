using System;
using System.IO;
using FlatRedBall2;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers reading Glue's per-entity movement CSVs. The mapping is close to 1:1, and every trap is in
// a unit change, a rename, or a column whose absence changes how the character feels.
public class GlueMovementTests
{
    private static string PlatformerCsv() =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "Content",
            "Entities", "Player", "PlatformerValuesStatic.csv"));

    [Fact]
    public void ReadPlatformer_DoorsDemo_ReadsEveryNamedRow()
    {
        var values = GlueMovementValues.ReadPlatformer(PlatformerCsv());

        values.Keys.ShouldContain("Ground");
        values.Keys.ShouldContain("Air");
        values.Keys.ShouldContain("Climbing");
    }

    [Fact]
    public void ReadPlatformer_GroundRow_MapsSpeedsGravityAndJump()
    {
        var ground = GlueMovementValues.ReadPlatformer(PlatformerCsv())["Ground"];

        ground.MaxSpeedX.ShouldBe(100f);
        ground.Gravity.ShouldBe(900f);
        ground.MaxFallSpeed.ShouldBe(500f);
        ground.JumpVelocity.ShouldBe(230f);
        ground.JumpApplyByButtonHold.ShouldBeTrue();
    }

    [Fact]
    public void ReadPlatformer_DurationColumns_BecomeTimeSpans()
    {
        // Glue stores plain seconds; FRB2's fields are TimeSpan, so a bare float will not compile
        // and a wrong conversion is silent.
        var ground = GlueMovementValues.ReadPlatformer(PlatformerCsv())["Ground"];

        ground.AccelerationTimeX.ShouldBe(TimeSpan.FromSeconds(0.25));
        ground.DecelerationTimeX.ShouldBe(TimeSpan.FromSeconds(0.15));
        ground.JumpApplyLength.ShouldBe(TimeSpan.FromSeconds(0.2));
    }

    [Fact]
    public void ReadPlatformer_DownhillBoostPercentage_BecomesAMultiplier()
    {
        // Renamed *and* rescaled: FRB1 stores a percentage, FRB2 a multiplier. 50 -> 1.5.
        var ground = GlueMovementValues.ReadPlatformer(PlatformerCsv())["Ground"];

        ground.DownhillMaxSpeedMultiplier.ShouldBe(1.5f, tolerance: 0.0001);
    }

    [Fact]
    public void ReadPlatformer_UsesAccelerationFalse_ZeroesBothDurations()
    {
        // FRB1 expresses "no easing" as a flag; FRB2 expresses it as a zero duration. Ignoring the
        // column smooths movement the author wanted to snap — a feel change no test would notice.
        string csv = "\"Name (System.String, required)\",MaxSpeedX (float)," +
                     "AccelerationTimeX (float),DecelerationTimeX (float),UsesAcceleration (bool)\n" +
                     "Snappy,100,0.5,0.5,False\n";

        var values = GlueMovementValues.ReadPlatformer(csv)["Snappy"];

        values.AccelerationTimeX.ShouldBe(TimeSpan.Zero);
        values.DecelerationTimeX.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ReadPlatformer_CloudPlatformColumn_MapsToTheRenamedFlag()
    {
        var ground = GlueMovementValues.ReadPlatformer(PlatformerCsv())["Ground"];

        ground.CanFallThroughOneWayCollision.ShouldBeTrue();
    }

    [Fact]
    public void TryParseRowReference_SplitsOnTheLastInSoARowNamedInStillResolves()
    {
        GlueMovementValues.TryParseRowReference(
            "Ground in PlatformerValuesStatic.csv", out string row, out string file).ShouldBeTrue();
        row.ShouldBe("Ground");
        file.ShouldBe("PlatformerValuesStatic.csv");

        GlueMovementValues.TryParseRowReference(
            "Walk in Water in Values.csv", out row, out _).ShouldBeTrue();
        row.ShouldBe("Walk in Water");

        // "<NULL>" is Glue's way of leaving a slot deliberately empty.
        GlueMovementValues.TryParseRowReference("<NULL>", out _, out _).ShouldBeFalse();
        GlueMovementValues.TryParseRowReference(null, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void BuildObjects_DoorsDemoPlayer_FillsItsPlatformerSlotsFromTheCsv()
    {
        // Player.glej names three CSV rows through GroundMovement / AirMovement / AfterDoubleJump.
        // AfterDoubleJump has no value, which is deliberate — it falls back to air movement.
        var project = GlueProject.Load(
            Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"));

        var entity = new GlueEntity
        {
            Save = project.FindEntity(@"Entities\Player"),
            Content = new GlueContentSource(
                new ContentLoader(), Path.Combine("Glue", "Fixtures", "DoorsDemo", "Content")),
        };

        entity.BuildObjects();

        entity.Platformer.GroundMovement.ShouldNotBeNull();
        entity.Platformer.GroundMovement!.MaxSpeedX.ShouldBe(100f);
        entity.Platformer.AirMovement.MaxFallSpeed.ShouldBe(260f);
        entity.Platformer.AfterDoubleJump.ShouldBeNull();
    }

    [Fact]
    public void ReadTopDown_DirectionColumns_AreSetExplicitlyNotLeftToADefault()
    {
        // These two default the opposite way on the two sides, so a CSV that omits them must not
        // inherit either side's default silently.
        string csv = "\"Name (string, required)\",MaxSpeed (float)," +
                     "UpdateDirectionFromInput (bool),UpdateDirectionFromVelocity (bool)\n" +
                     "Default,76,True,False\n";

        var values = GlueMovementValues.ReadTopDown(csv)["Default"];

        values.MaxSpeed.ShouldBe(76f);
        values.UpdateDirectionFromInput.ShouldBeTrue();
        values.UpdateDirectionFromVelocity.ShouldBeFalse();
    }
}
