using System;
using System.Collections.Generic;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Input;

/// <summary>Stands in for MonoGame's GamePad so connection, vibration, and deadzone run headless.</summary>
internal sealed class FakeGamepadPlatform : IGamepadPlatform
{
    public bool Connected { get; set; } = true;
    public Vector2 LeftStick { get; set; }
    public Vector2 RightStick { get; set; }
    public float LeftTrigger { get; set; }
    public GamePadDeadZone? LastDeadZone { get; private set; }
    public List<(int Index, float Left, float Right)> Vibrations { get; } = new();
    public bool VibrationResult { get; set; } = true;

    public GamePadState GetState(int index, GamePadDeadZone deadZone)
    {
        LastDeadZone = deadZone;
        if (!Connected) return default;
        return new GamePadState(
            new GamePadThumbSticks(LeftStick, RightStick),
            new GamePadTriggers(LeftTrigger, 0f),
            new GamePadButtons(0),
            new GamePadDPad());
    }

    public bool SetVibration(int index, float leftMotor, float rightMotor)
    {
        Vibrations.Add((index, leftMotor, rightMotor));
        return VibrationResult;
    }
}

public class GamepadConnectionTests
{
    [Fact]
    public void IsConnected_PlatformDisconnected_ReturnsFalse()
    {
        var gamepad = new Gamepad(0, new FakeGamepadPlatform { Connected = false });
        gamepad.Update();

        gamepad.IsConnected.ShouldBeFalse();
    }

    [Fact]
    public void IsConnected_PlatformConnected_ReturnsTrue()
    {
        var gamepad = new Gamepad(0, new FakeGamepadPlatform { Connected = true });
        gamepad.Update();

        gamepad.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public void IsConnected_InjectedInput_ReturnsTrue()
    {
        // Automation and tests drive a pad with no hardware attached; it must read as present.
        var gamepad = new Gamepad(0, new FakeGamepadPlatform { Connected = false });
        gamepad.InjectButton(Buttons.A, down: true);
        gamepad.Update();

        gamepad.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public void GamepadConnectionChanged_PadConnectsThenDisconnects_RaisesOncePerChange()
    {
        var platform = new FakeGamepadPlatform { Connected = false };
        var manager = new InputManager(platform);
        var events = new List<(int Index, bool Connected)>();
        manager.GamepadConnectionChanged += (index, connected) => events.Add((index, connected));

        manager.Update(TimeSpan.Zero);
        platform.Connected = true;
        manager.Update(TimeSpan.FromSeconds(0.01));
        manager.Update(TimeSpan.FromSeconds(0.02));
        platform.Connected = false;
        manager.Update(TimeSpan.FromSeconds(0.03));

        // All four slots share the fake, so each change fires once per index.
        events.Count.ShouldBe(8);
        events[0].ShouldBe((0, true));
        events[4].ShouldBe((0, false));
    }

    [Fact]
    public void GamepadConnectionChanged_PadAlreadyConnectedAtStartup_RaisesOnFirstUpdate()
    {
        var manager = new InputManager(new FakeGamepadPlatform { Connected = true });
        var count = 0;
        manager.GamepadConnectionChanged += (_, connected) => { if (connected) count++; };

        manager.Update(TimeSpan.Zero);

        count.ShouldBe(4);
    }
}

public class GamepadVibrationTests
{
    [Fact]
    public void SetVibration_ForwardsMotorsToPlatformAtPadIndex()
    {
        var platform = new FakeGamepadPlatform();
        var gamepad = new Gamepad(2, platform);

        var result = gamepad.SetVibration(0.25f, 0.75f);

        result.ShouldBeTrue();
        platform.Vibrations.ShouldBe(new[] { (2, 0.25f, 0.75f) });
    }

    [Fact]
    public void SetVibration_OutOfRangeValues_ClampsToZeroOne()
    {
        var platform = new FakeGamepadPlatform();
        var gamepad = new Gamepad(0, platform);

        gamepad.SetVibration(-1f, 3f);

        platform.Vibrations.ShouldBe(new[] { (0, 0f, 1f) });
    }

    [Fact]
    public void SetVibration_PlatformRejects_ReturnsFalse()
    {
        var gamepad = new Gamepad(0, new FakeGamepadPlatform { VibrationResult = false });

        gamepad.SetVibration(1f, 1f).ShouldBeFalse();
    }
}

public class GamepadDeadzoneTests
{
    [Fact]
    public void GetAxis_DefaultDeadzone_UsesMonoGameIndependentAxes()
    {
        // The pre-existing behavior: let MonoGame apply its own per-axis thresholds.
        var platform = new FakeGamepadPlatform { LeftStick = new Vector2(0.9f, 0.1f) };
        var gamepad = new Gamepad(0, platform);
        gamepad.Update();

        platform.LastDeadZone.ShouldBe(GamePadDeadZone.IndependentAxes);
        gamepad.GetAxis(GamepadAxis.LeftStickX).ShouldBe(0.9f);
        gamepad.Deadzone.ShouldBeNull();
        gamepad.DeadzoneType.ShouldBe(GamepadDeadzoneType.PerAxis);
    }

    [Fact]
    public void GetAxis_RadialTypeWithDefaultDeadzone_UsesMonoGameCircular()
    {
        var platform = new FakeGamepadPlatform();
        var gamepad = new Gamepad(0, platform) { DeadzoneType = GamepadDeadzoneType.Radial };
        gamepad.Update();

        platform.LastDeadZone.ShouldBe(GamePadDeadZone.Circular);
    }

    [Fact]
    public void GetAxis_PerAxisDeadzone_ZeroesSmallAxisAndRescalesLargeOne()
    {
        var platform = new FakeGamepadPlatform { LeftStick = new Vector2(0.6f, 0.1f), RightStick = new Vector2(-0.6f, 1f) };
        var gamepad = new Gamepad(0, platform) { Deadzone = 0.2f, DeadzoneType = GamepadDeadzoneType.PerAxis };
        gamepad.Update();

        platform.LastDeadZone.ShouldBe(GamePadDeadZone.None);
        gamepad.GetAxis(GamepadAxis.LeftStickX).ShouldBe(0.5f, tolerance: 0.0001f); // (0.6 - 0.2) / (1 - 0.2)
        gamepad.GetAxis(GamepadAxis.LeftStickY).ShouldBe(0f);
        gamepad.GetAxis(GamepadAxis.RightStickX).ShouldBe(-0.5f, tolerance: 0.0001f);
        gamepad.GetAxis(GamepadAxis.RightStickY).ShouldBe(1f, tolerance: 0.0001f);
    }

    [Fact]
    public void GetAxis_RadialDeadzone_ZeroesStickInsideCircleAndKeepsOneOutside()
    {
        // (0.2, 0.2) is inside a per-axis 0.15 band on neither axis but this is radial 0.25:
        // length 0.283 clears the circle, while (0.15, 0.15) has length 0.212 and does not.
        var platform = new FakeGamepadPlatform { LeftStick = new Vector2(0.15f, 0.15f), RightStick = new Vector2(0.2f, 0.2f) };
        var gamepad = new Gamepad(0, platform) { Deadzone = 0.25f, DeadzoneType = GamepadDeadzoneType.Radial };
        gamepad.Update();

        gamepad.GetAxis(GamepadAxis.LeftStickX).ShouldBe(0f);
        gamepad.GetAxis(GamepadAxis.LeftStickY).ShouldBe(0f);
        gamepad.GetAxis(GamepadAxis.RightStickX).ShouldBeGreaterThan(0f);
        gamepad.GetAxis(GamepadAxis.RightStickY).ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void GetAxis_RadialDeadzone_PreservesDirectionAndRescalesLength()
    {
        var platform = new FakeGamepadPlatform { LeftStick = new Vector2(0.6f, 0f) };
        var gamepad = new Gamepad(0, platform) { Deadzone = 0.2f, DeadzoneType = GamepadDeadzoneType.Radial };
        gamepad.Update();

        gamepad.GetAxis(GamepadAxis.LeftStickX).ShouldBe(0.5f, tolerance: 0.0001f);
        gamepad.GetAxis(GamepadAxis.LeftStickY).ShouldBe(0f);
    }

    [Fact]
    public void GetAxis_CustomDeadzone_DoesNotAffectTriggers()
    {
        var platform = new FakeGamepadPlatform { LeftTrigger = 0.1f };
        var gamepad = new Gamepad(0, platform) { Deadzone = 0.5f };
        gamepad.Update();

        gamepad.GetAxis(GamepadAxis.LeftTrigger).ShouldBe(0.1f, tolerance: 0.0001f);
    }

    [Fact]
    public void GetAxis_InjectedAxisWithCustomDeadzone_AppliesDeadzone()
    {
        var gamepad = new Gamepad(0, new FakeGamepadPlatform()) { Deadzone = 0.2f };
        gamepad.InjectAxis(GamepadAxis.LeftStickX, 0.1f);
        gamepad.Update();

        gamepad.GetAxis(GamepadAxis.LeftStickX).ShouldBe(0f);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void Deadzone_OutOfRange_Throws(float value)
    {
        var gamepad = new Gamepad(0, new FakeGamepadPlatform());

        Should.Throw<ArgumentOutOfRangeException>(() => gamepad.Deadzone = value);
    }
}
