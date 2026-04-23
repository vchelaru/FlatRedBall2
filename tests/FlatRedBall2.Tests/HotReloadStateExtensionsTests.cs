using FlatRedBall2;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class HotReloadStateExtensionsTests
{
    private class PlayerStub : Entity { }
    private class EnemyStub : Entity { }

    [Fact]
    public void PreserveThenRestore_Entity_RoundTripsAllEightValues()
    {
        var saved = new PlayerStub
        {
            X = 10f, Y = 20f,
            VelocityX = 1f, VelocityY = 2f,
            AccelerationX = 0.5f, AccelerationY = -0.25f,
            Rotation = Angle.FromDegrees(30f),
            RotationVelocity = Angle.FromDegrees(5f),
        };
        var state = new HotReloadState();
        state.Preserve(saved);

        var restored = new PlayerStub();
        state.Restore(restored);

        restored.X.ShouldBe(10f);
        restored.Y.ShouldBe(20f);
        restored.VelocityX.ShouldBe(1f);
        restored.VelocityY.ShouldBe(2f);
        restored.AccelerationX.ShouldBe(0.5f);
        restored.AccelerationY.ShouldBe(-0.25f);
        restored.Rotation.Degrees.ShouldBe(30f, tolerance: 0.001f);
        restored.RotationVelocity.Degrees.ShouldBe(5f, tolerance: 0.001f);
    }

    [Fact]
    public void PreserveThenRestore_Camera_RoundTripsAllSixValues()
    {
        var saved = new Camera
        {
            X = 100f, Y = 200f,
            VelocityX = 3f, VelocityY = 4f,
            AccelerationX = 0.1f, AccelerationY = 0.2f,
        };
        var state = new HotReloadState();
        state.Preserve(saved);

        var restored = new Camera();
        state.Restore(restored);

        restored.X.ShouldBe(100f);
        restored.Y.ShouldBe(200f);
        restored.VelocityX.ShouldBe(3f);
        restored.VelocityY.ShouldBe(4f);
        restored.AccelerationX.ShouldBe(0.1f);
        restored.AccelerationY.ShouldBe(0.2f);
    }

    [Fact]
    public void AutoKey_TwoSameTypeEntities_GetIndependentState()
    {
        var p1Saved = new PlayerStub { X = 10f };
        var p2Saved = new PlayerStub { X = 20f };
        var state = new HotReloadState();
        state.Preserve(p1Saved);
        state.Preserve(p2Saved);

        var p1Restored = new PlayerStub();
        var p2Restored = new PlayerStub();
        state.Restore(p1Restored);
        state.Restore(p2Restored);

        p1Restored.X.ShouldBe(10f);
        p2Restored.X.ShouldBe(20f);
    }

    [Fact]
    public void AutoKey_InterleavedTypes_EachTypeHasOwnCounter()
    {
        var player = new PlayerStub { X = 1f };
        var enemy = new EnemyStub { X = 100f };
        var player2 = new PlayerStub { X = 2f };
        var state = new HotReloadState();
        state.Preserve(player);
        state.Preserve(enemy);
        state.Preserve(player2);

        var pR = new PlayerStub();
        var eR = new EnemyStub();
        var p2R = new PlayerStub();
        state.Restore(pR);
        state.Restore(eR);
        state.Restore(p2R);

        pR.X.ShouldBe(1f);
        eR.X.ShouldBe(100f);
        p2R.X.ShouldBe(2f);
    }

    [Fact]
    public void ExplicitName_UsesProvidedKey()
    {
        var saved = new PlayerStub { X = 42f };
        var state = new HotReloadState();
        state.Preserve(saved, "heroA");

        // Also save an auto-keyed entity to confirm named and auto don't cross-talk.
        var auto = new PlayerStub { X = 7f };
        state.Preserve(auto);

        var fromName = new PlayerStub();
        state.Restore(fromName, "heroA");
        fromName.X.ShouldBe(42f);

        var fromAuto = new PlayerStub();
        state.Restore(fromAuto);
        fromAuto.X.ShouldBe(7f);
    }

    [Fact]
    public void Restore_NoSavedStateForEntity_LeavesValuesUntouched()
    {
        // First-run scenario: nothing was ever saved, so Restore is a silent no-op and the
        // entity keeps whatever values CustomInitialize assigned.
        var entity = new PlayerStub { X = 99f, Y = 88f, Rotation = Angle.FromDegrees(15f) };
        var state = new HotReloadState();

        state.Restore(entity);

        entity.X.ShouldBe(99f);
        entity.Y.ShouldBe(88f);
        entity.Rotation.Degrees.ShouldBe(15f, tolerance: 0.001f);
    }

    [Fact]
    public void SaveAndRestoreCounters_AreIndependent()
    {
        // Save phase and restore phase each get their own counter. The same HotReloadState
        // instance being used for both phases (as would happen if tests share one) must not
        // cross-contaminate.
        var state = new HotReloadState();
        var saved = new PlayerStub { X = 5f };
        state.Preserve(saved); // uses save counter → "PlayerStub_1"

        var restored = new PlayerStub();
        state.Restore(restored); // uses restore counter → "PlayerStub_1"

        restored.X.ShouldBe(5f);
    }

    [Fact]
    public void ExplicitName_DoesNotAdvanceAutoCounter()
    {
        // Mixing named and auto should not cause the auto counter to skip indices. The named
        // save uses "heroA"; the subsequent auto save should be "PlayerStub_1", not _2.
        var state = new HotReloadState();
        var named = new PlayerStub { X = 50f };
        var auto = new PlayerStub { X = 60f };
        state.Preserve(named, "heroA");
        state.Preserve(auto);

        var namedR = new PlayerStub();
        var autoR = new PlayerStub();
        state.Restore(namedR, "heroA");
        state.Restore(autoR);

        namedR.X.ShouldBe(50f);
        autoR.X.ShouldBe(60f);
    }
}
